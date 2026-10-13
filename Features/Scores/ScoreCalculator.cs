using HabitSystem.Common;
using HabitSystem.Domain;
using HabitSystem.Domain.Enums;
using HabitSystem.Infrastructure;
using MediatR;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace HabitSystem.Features.Scores;

/// <summary>
/// Calculates daily scores based on habits and check-ins.
///
/// Rules (per date D, in the user's timezone):
/// - A habit counts on D only if it was created on or before D (CreatedAt converted to the user's
///   local date) and it is active, or it was archived after D (ArchivedAt local date &gt; D).
///   Archived habits with no ArchivedAt (legacy rows) never count — same as the pre-v1.0 behaviour.
/// - Daily habits are expected every day; SpecificDays habits on FrequencyDays
///   (0=Sunday..6=Saturday, legacy 7 also treated as Sunday); XTimesWeek habits only when checked in.
/// - Only habits that count toward TotalPossible can add to TotalEarned, so the score never exceeds 100%.
/// - Percentage is clamped to 0..100.
/// </summary>
public class ScoreCalculator
{
    private readonly AppDbContext _db;

    public ScoreCalculator(AppDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// Computes scores for every date in [startDate, endDate] without writing to the database
    /// </summary>
    public async Task<List<DailyScore>> ComputeScores(DateOnly startDate, DateOnly endDate, Guid userId, CancellationToken cancellationToken = default)
    {
        var timeZone = await _db.GetUserTimeZoneAsync(userId, cancellationToken);

        // All habits (including archived) — filtered per date below
        var habits = await _db.Habits
            .AsNoTracking()
            .Where(h => h.UserId == userId)
            .ToListAsync(cancellationToken);

        var checkIns = await _db.CheckIns
            .AsNoTracking()
            .Where(c => c.UserId == userId && c.Date >= startDate && c.Date <= endDate)
            .ToListAsync(cancellationToken);

        var habitWindows = habits.ToDictionary(h => h.Id, h => (
            Habit: h,
            CreatedOn: UserClock.ToLocalDate(h.CreatedAt, timeZone),
            ArchivedOn: h.ArchivedAt.HasValue ? UserClock.ToLocalDate(h.ArchivedAt.Value, timeZone) : (DateOnly?)null));

        var results = new List<DailyScore>();
        var now = DateTime.UtcNow;

        for (var date = startDate; date <= endDate; date = date.AddDays(1))
        {
            var dayCheckIns = checkIns.Where(c => c.Date == date).ToDictionary(c => c.HabitId);

            int totalPossible = 0;
            int totalEarned = 0;

            foreach (var (habit, createdOn, archivedOn) in habitWindows.Values)
            {
                if (!CountsOnDate(habit, createdOn, archivedOn, date))
                    continue;

                dayCheckIns.TryGetValue(habit.Id, out var checkIn);

                var expected = IsHabitExpectedOnDate(habit, date)
                    // XTimesWeek habits only count if user did a check-in
                    || (habit.FrequencyType == FrequencyType.XTimesWeek && checkIn != null);

                if (!expected)
                    continue;

                totalPossible += habit.Weight;
                if (checkIn != null)
                    totalEarned += Math.Min(GetEarnedWeight(habit, checkIn.Status), (int)habit.Weight);
            }

            decimal percentage = totalPossible > 0 ? (decimal)totalEarned / totalPossible * 100 : 0;
            percentage = Math.Clamp(Math.Round(percentage, 2), 0m, 100m);

            results.Add(new DailyScore
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Date = date,
                TotalPossible = (short)Math.Min(totalPossible, short.MaxValue),
                TotalEarned = (short)Math.Min(totalEarned, short.MaxValue),
                Percentage = percentage,
                CalculatedAt = now
            });
        }

        return results;
    }

    /// <summary>
    /// Computes the score for a single date without writing to the database
    /// </summary>
    public async Task<DailyScore> ComputeScore(DateOnly date, Guid userId, CancellationToken cancellationToken = default)
        => (await ComputeScores(date, date, userId, cancellationToken))[0];

    /// <summary>
    /// Calculates the daily score for a given date and user and upserts the cached DailyScore row
    /// </summary>
    public async Task<DailyScore> CalculateScore(DateOnly date, Guid userId, CancellationToken cancellationToken = default)
    {
        var computed = await ComputeScore(date, userId, cancellationToken);

        for (var attempt = 0; ; attempt++)
        {
            // Find or create daily score
            var dailyScore = await _db.DailyScores
                .FirstOrDefaultAsync(d => d.UserId == userId && d.Date == date, cancellationToken);

            if (dailyScore == null)
            {
                dailyScore = computed;
                _db.DailyScores.Add(dailyScore);
            }
            else
            {
                // Update existing score
                dailyScore.TotalPossible = computed.TotalPossible;
                dailyScore.TotalEarned = computed.TotalEarned;
                dailyScore.Percentage = computed.Percentage;
                dailyScore.CalculatedAt = computed.CalculatedAt;
            }

            try
            {
                await _db.SaveChangesAsync(cancellationToken);
                return dailyScore;
            }
            catch (DbUpdateException ex) when (attempt == 0 && ex.InnerException is SqliteException { SqliteErrorCode: 19 })
            {
                // A concurrent recalculation inserted the same (UserId, Date): retry as an update
                _db.Entry(dailyScore).State = EntityState.Detached;
            }
        }
    }

    /// <summary>
    /// A habit counts on a date if it existed (created on/before) and was not archived on/before that date
    /// </summary>
    private static bool CountsOnDate(Habit habit, DateOnly createdOn, DateOnly? archivedOn, DateOnly date)
    {
        if (date < createdOn)
            return false;

        if (habit.IsActive)
            return true;

        // Legacy archived rows without ArchivedAt: excluded from every date
        return archivedOn.HasValue && archivedOn.Value > date;
    }

    /// <summary>
    /// Determines if a habit is expected on a given date based on its frequency.
    /// Days use the JS getDay() convention: 0=Sunday..6=Saturday (7 also accepted as Sunday).
    /// </summary>
    private static bool IsHabitExpectedOnDate(Habit habit, DateOnly date)
    {
        return habit.FrequencyType switch
        {
            FrequencyType.Daily => true,

            FrequencyType.SpecificDays =>
                habit.FrequencyDays != null &&
                habit.FrequencyDays.Any(d => (d == 7 ? 0 : d) == (int)date.DayOfWeek),

            // XTimesWeek: Not expected on any specific day (user chooses when)
            // These habits only count in the score when a check-in is made
            FrequencyType.XTimesWeek => false,

            _ => false
        };
    }

    /// <summary>
    /// Gets the earned weight for a check-in based on its status
    /// </summary>
    private static int GetEarnedWeight(Habit habit, CheckInStatus status)
    {
        return status switch
        {
            CheckInStatus.Done => habit.Weight,
            CheckInStatus.Partial => habit.PartialWeight,
            CheckInStatus.Skipped => 0,
            _ => 0
        };
    }
}

// Handler for recalculating score (called after check-in and habit create/update/archive/restore)
public class RecalculateScoreHandler : IRequestHandler<RecalculateScoreCommand, Result>
{
    private readonly AppDbContext _db;
    private readonly ILogger<RecalculateScoreHandler> _logger;

    public RecalculateScoreHandler(AppDbContext db, ILogger<RecalculateScoreHandler> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<Result> Handle(RecalculateScoreCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var calculator = new ScoreCalculator(_db);
            await calculator.CalculateScore(request.Date, request.UserId, cancellationToken);
            return Result.Success();
        }
        catch (DbUpdateException ex)
        {
            // The cached score is not authoritative (GET endpoints recompute), so never fail the caller
            _logger.LogWarning(ex, "Failed to persist score for user {UserId} on {Date}", request.UserId, request.Date);
            return Result.Failure("Failed to persist score", ErrorCodes.InternalError);
        }
    }
}

public record RecalculateScoreCommand(DateOnly Date, Guid UserId) : IRequest<Result>;
