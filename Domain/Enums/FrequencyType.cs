namespace HabitSystem.Domain.Enums;

/// <summary>
/// Defines how frequently a habit should be performed
/// </summary>
public enum FrequencyType
{
    /// <summary>
    /// Habit should be done every day
    /// </summary>
    Daily = 0,

    /// <summary>
    /// Habit should be done on specific days of the week
    /// (stored in FrequencyDays array: 1=Monday, 7=Sunday)
    /// </summary>
    SpecificDays = 1,

    /// <summary>
    /// Habit should be done X times per week
    /// (stored in FrequencyTimes property)
    /// </summary>
    XTimesWeek = 2
}
