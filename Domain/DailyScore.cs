namespace HabitSystem.Domain;

/// <summary>
/// Represents a cached calculation of daily score
/// This is computed when check-ins are created/updated to ensure fast queries
/// </summary>
public class DailyScore
{
    /// <summary>
    /// Unique identifier
    /// </summary>
    public Guid Id { get; init; }

    /// <summary>
    /// The user this score belongs to
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// Date of the score
    /// There can only be one score per user per date
    /// </summary>
    public DateOnly Date { get; set; }

    /// <summary>
    /// Total possible score for this day
    /// (sum of weights of all habits expected on this day based on their frequency)
    /// </summary>
    public short TotalPossible { get; set; }

    /// <summary>
    /// Total earned score for this day
    /// (sum of earned weights from check-ins: full weight for Done, partial weight for Partial, 0 for Skipped)
    /// </summary>
    public short TotalEarned { get; set; }

    /// <summary>
    /// Percentage of completion (TotalEarned / TotalPossible * 100)
    /// </summary>
    public decimal Percentage { get; set; }

    /// <summary>
    /// When this score was last calculated
    /// </summary>
    public DateTime CalculatedAt { get; set; }

    // Navigation property
    /// <summary>
    /// The user this score belongs to
    /// </summary>
    public User User { get; set; } = null!;
}
