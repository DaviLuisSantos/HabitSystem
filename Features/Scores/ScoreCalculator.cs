using HabitSystem.Common;
using HabitSystem.Domain;
using HabitSystem.Domain.Enums;
using HabitSystem.Infrastructure;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HabitSystem.Features.Scores;

/// <summary>
/// Calculates and updates daily scores based on check-ins
/// </summary>
public class ScoreCalculator
{
    private readonly AppDbContext _db;

    public ScoreCalculator(AppDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// Calculates the daily score for a given date
    /// </summary>
    public async Task<DailyScore> CalculateScore(DateOnly date, CancellationToken cancellationToken = default)
    {
        // Get all active habits for the user
        var habits = await _db.Habits
            .Where(h => h.UserId == Constants.DefaultUserId && h.IsActive)
            .ToListAsync(cancellationToken);

        // Calculate total possible score (sum of weights of expected habits)
        short totalPossible = 0;
        foreach (var habit in habits)
        {
            if (IsHabitExpectedOnDate(habit, date))
            {
                totalPossible += habit.Weight;
            }
        }

        // Get check-ins for this date
        var checkIns = await _db.CheckIns
            .Include(c => c.Habit)
            .Where(c => c.UserId == Constants.DefaultUserId && c.Date == date)
            .ToListAsync(cancellationToken);

        // Calculate total earned score
        short totalEarned = 0;
        foreach (var checkIn in checkIns)
        {
            totalEarned += GetEarnedWeight(checkIn);
        }

        // Calculate percentage
        decimal percentage = totalPossible > 0 ? (decimal)totalEarned / totalPossible * 100 : 0;

        // Find or create daily score
        var dailyScore = await _db.DailyScores
            .FirstOrDefaultAsync(d => d.UserId == Constants.DefaultUserId && d.Date == date, cancellationToken);

        if (dailyScore == null)
        {
            dailyScore = new DailyScore
            {
                Id = Guid.NewGuid(),
                UserId = Constants.DefaultUserId,
                Date = date,
                TotalPossible = totalPossible,
                TotalEarned = totalEarned,
                Percentage = Math.Round(percentage, 2),
                CalculatedAt = DateTime.UtcNow
            };
            _db.DailyScores.Add(dailyScore);
        }
        else
        {
            // Update existing score
            dailyScore.TotalPossible = totalPossible;
            dailyScore.TotalEarned = totalEarned;
            dailyScore.Percentage = Math.Round(percentage, 2);
            dailyScore.CalculatedAt = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync(cancellationToken);

        return dailyScore;
    }

    /// <summary>
    /// Determines if a habit is expected on a given date based on its frequency
    /// </summary>
    private bool IsHabitExpectedOnDate(Habit habit, DateOnly date)
    {
        return habit.FrequencyType switch
        {
            FrequencyType.Daily => true,
            
            FrequencyType.SpecificDays => 
                habit.FrequencyDays != null && 
                habit.FrequencyDays.Contains((int)date.DayOfWeek == 0 ? 7 : (int)date.DayOfWeek),
            
            // For XTimesWeek, consider all days as possible (user decides when)
            FrequencyType.XTimesWeek => true,
            
            _ => false
        };
    }

    /// <summary>
    /// Gets the earned weight for a check-in based on its status
    /// </summary>
    private short GetEarnedWeight(CheckIn checkIn)
    {
        return checkIn.Status switch
        {
            CheckInStatus.Done => checkIn.Habit.Weight,
            CheckInStatus.Partial => checkIn.Habit.PartialWeight,
            CheckInStatus.Skipped => 0,
            _ => 0
        };
    }
}

// Handler for recalculating score (called after check-in create/update)
public class RecalculateScoreHandler : IRequestHandler<RecalculateScoreCommand, Result>
{
    private readonly AppDbContext _db;

    public RecalculateScoreHandler(AppDbContext db)
    {
        _db = db;
    }

    public async Task<Result> Handle(RecalculateScoreCommand request, CancellationToken cancellationToken)
    {
        var calculator = new ScoreCalculator(_db);
        await calculator.CalculateScore(request.Date, cancellationToken);
        return Result.Success();
    }
}

public record RecalculateScoreCommand(DateOnly Date) : IRequest<Result>;
