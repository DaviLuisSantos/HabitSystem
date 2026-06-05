using HabitSystem.Domain.Enums;

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
    /// Hashed password for authentication
    /// </summary>
    public string PasswordHash { get; set; } = string.Empty;

    /// <summary>
    /// Current refresh token for this user
    /// </summary>
    public string? RefreshToken { get; set; }

    /// <summary>
    /// When the refresh token expires
    /// </summary>
    public DateTime? RefreshTokenExpiryTime { get; set; }

    /// <summary>
    /// Subscription plan (Free or Pro)
    /// </summary>
    public Plan Plan { get; set; } = Plan.Free;

    /// <summary>
    /// Whether the user's email has been verified
    /// </summary>
    public bool IsEmailVerified { get; set; } = false;

    /// <summary>
    /// Token sent to user's email for verification
    /// </summary>
    public string? EmailVerificationToken { get; set; }

    /// <summary>
    /// When the email verification token expires
    /// </summary>
    public DateTime? EmailVerificationTokenExpiry { get; set; }

    /// <summary>
    /// Token sent to user's email for password reset
    /// </summary>
    public string? PasswordResetToken { get; set; }

    /// <summary>
    /// When the password reset token expires
    /// </summary>
    public DateTime? PasswordResetTokenExpiry { get; set; }

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
