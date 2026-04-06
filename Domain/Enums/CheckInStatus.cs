namespace HabitSystem.Domain.Enums;

/// <summary>
/// Status of a check-in for a habit
/// </summary>
public enum CheckInStatus
{
    /// <summary>
    /// Habit was completed fully (earns full weight)
    /// </summary>
    Done = 0,

    /// <summary>
    /// Habit was completed partially (earns partial weight)
    /// Implements the "Minimum Viable Mode" concept
    /// </summary>
    Partial = 1,

    /// <summary>
    /// Habit was not done (earns 0 points)
    /// </summary>
    Skipped = 2
}
