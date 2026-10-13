using HabitSystem.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace HabitSystem.Common;

/// <summary>
/// Resolves dates in the user's local timezone (User.Timezone, IANA id such as "America/Sao_Paulo").
/// Falls back to <see cref="DefaultTimezone"/>, then UTC, when the id is unknown.
/// </summary>
public static class UserClock
{
    public const string DefaultTimezone = "America/Sao_Paulo";

    /// <summary>
    /// Returns true if the id is a timezone known to this machine (IANA or Windows id)
    /// </summary>
    public static bool IsValidTimeZone(string? timezoneId)
    {
        if (string.IsNullOrWhiteSpace(timezoneId))
            return false;

        try
        {
            TimeZoneInfo.FindSystemTimeZoneById(timezoneId);
            return true;
        }
        catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            return false;
        }
    }

    /// <summary>
    /// Resolves a timezone id, falling back to America/Sao_Paulo and then UTC
    /// </summary>
    public static TimeZoneInfo ResolveTimeZone(string? timezoneId)
    {
        foreach (var id in new[] { timezoneId, DefaultTimezone })
        {
            if (string.IsNullOrWhiteSpace(id))
                continue;

            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(id);
            }
            catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
            {
                // try the next candidate
            }
        }

        return TimeZoneInfo.Utc;
    }

    /// <summary>
    /// Converts a UTC instant (Kind may be Unspecified when read from SQLite) to the local date in a timezone
    /// </summary>
    public static DateOnly ToLocalDate(DateTime utc, TimeZoneInfo timeZone)
    {
        var asUtc = utc.Kind == DateTimeKind.Utc ? utc : DateTime.SpecifyKind(utc, DateTimeKind.Utc);
        return DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(asUtc, timeZone));
    }

    /// <summary>
    /// "Today" in the given timezone
    /// </summary>
    public static DateOnly Today(TimeZoneInfo timeZone) => ToLocalDate(DateTime.UtcNow, timeZone);

    /// <summary>
    /// Loads the user's timezone from the database (with fallbacks)
    /// </summary>
    public static async Task<TimeZoneInfo> GetUserTimeZoneAsync(this AppDbContext db, Guid userId, CancellationToken ct = default)
    {
        var timezoneId = await db.Users
            .Where(u => u.Id == userId)
            .Select(u => u.Timezone)
            .FirstOrDefaultAsync(ct);

        return ResolveTimeZone(timezoneId);
    }

    /// <summary>
    /// "Today" for the given user, based on User.Timezone
    /// </summary>
    public static async Task<DateOnly> GetUserTodayAsync(this AppDbContext db, Guid userId, CancellationToken ct = default)
        => Today(await db.GetUserTimeZoneAsync(userId, ct));
}
