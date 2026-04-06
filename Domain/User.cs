namespace HabitSystem.Domain;

/// <summary>
/// Represents a user in the system
/// </summary>
public class User
{
    /// <summary>
    /// Unique identifier
    /// </summary>
    public Guid Id { get; init; }

    /// <summary>
    /// User's full name
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// User's email address
    /// </summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// User's timezone (e.g., "America/Sao_Paulo")
    /// Used for correct date calculations
    /// </summary>
    public string Timezone { get; set; } = "America/Sao_Paulo";

    /// <summary>
    /// When the user account was created
    /// </summary>
    public DateTime CreatedAt { get; init; }

    // Navigation properties
    /// <summary>
    /// Habits owned by this user
    /// </summary>
    public ICollection<Habit> Habits { get; set; } = new List<Habit>();

    /// <summary>
    /// Check-ins made by this user
    /// </summary>
    public ICollection<CheckIn> CheckIns { get; set; } = new List<CheckIn>();

    /// <summary>
    /// Daily scores calculated for this user
    /// </summary>
    public ICollection<DailyScore> DailyScores { get; set; } = new List<DailyScore>();
}
