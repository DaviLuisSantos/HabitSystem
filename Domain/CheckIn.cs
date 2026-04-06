using HabitSystem.Domain.Enums;

namespace HabitSystem.Domain;

/// <summary>
/// Represents a daily check-in for a habit
/// </summary>
public class CheckIn
{
    /// <summary>
    /// Unique identifier
    /// </summary>
    public Guid Id { get; init; }

    /// <summary>
    /// The habit this check-in is for
    /// </summary>
    public Guid HabitId { get; set; }

    /// <summary>
    /// The user who made this check-in
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// Date of the check-in
    /// There can only be one check-in per habit per date
    /// </summary>
    public DateOnly Date { get; set; }

    /// <summary>
    /// Status of completion
    /// </summary>
    public CheckInStatus Status { get; set; }

    /// <summary>
    /// Optional note about this check-in
    /// </summary>
    public string? Note { get; set; }

    /// <summary>
    /// When this check-in was created
    /// </summary>
    public DateTime CreatedAt { get; init; }

    // Navigation properties
    /// <summary>
    /// The habit this check-in belongs to
    /// </summary>
    public Habit Habit { get; set; } = null!;

    /// <summary>
    /// The user who made this check-in
    /// </summary>
    public User User { get; set; } = null!;
}
