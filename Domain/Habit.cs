using HabitSystem.Domain.Enums;

namespace HabitSystem.Domain;

/// <summary>
/// Represents a habit that a user wants to track
/// </summary>
public class Habit
{
    /// <summary>
    /// Unique identifier
    /// </summary>
    public Guid Id { get; init; }

    /// <summary>
    /// Owner of this habit
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// Name of the habit (e.g., "Exercise", "Read")
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Optional description
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Weight/importance of this habit (1-10)
    /// Used to calculate daily score when completed fully
    /// </summary>
    public short Weight { get; set; } = 5;

    /// <summary>
    /// Weight earned when habit is completed partially (0-10)
    /// Implements the "Minimum Viable Mode" concept
    /// </summary>
    public short PartialWeight { get; set; } = 2;

    /// <summary>
    /// How frequently this habit should be done
    /// </summary>
    public FrequencyType FrequencyType { get; set; } = FrequencyType.Daily;

    /// <summary>
    /// Days of the week when habit should be done (if FrequencyType = SpecificDays)
    /// Values: 1=Monday, 2=Tuesday, ..., 7=Sunday
    /// Example: [1, 3, 5] = Monday, Wednesday, Friday
    /// </summary>
    public int[]? FrequencyDays { get; set; }

    /// <summary>
    /// Number of times per week habit should be done (if FrequencyType = XTimesWeek)
    /// Example: 3 = 3 times per week
    /// </summary>
    public short? FrequencyTimes { get; set; }

    /// <summary>
    /// Whether this habit is currently active
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// When this habit was created
    /// </summary>
    public DateTime CreatedAt { get; init; }

    /// <summary>
    /// When this habit was archived (soft delete)
    /// Null if habit is still active
    /// </summary>
    public DateTime? ArchivedAt { get; set; }

    // Navigation properties
    /// <summary>
    /// The user who owns this habit
    /// </summary>
    public User User { get; set; } = null!;

    /// <summary>
    /// Check-ins for this habit
    /// </summary>
    public ICollection<CheckIn> CheckIns { get; set; } = new List<CheckIn>();
}
