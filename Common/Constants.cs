namespace HabitSystem.Common;

/// <summary>
/// Application-wide constants
/// </summary>
public static class Constants
{
    /// <summary>
    /// Password reset token validity in hours
    /// </summary>
    public const int PasswordResetTokenExpiryHours = 2;

    /// <summary>
    /// Email verification token validity in hours
    /// </summary>
    public const int EmailVerificationTokenExpiryHours = 24;

    /// <summary>
    /// Minimum password length (register, reset, change)
    /// </summary>
    public const int MinPasswordLength = 8;

    /// <summary>
    /// Check-ins older than this many days (relative to the user's today) are rejected
    /// </summary>
    public const int MaxCheckInAgeDays = 365;

    /// <summary>
    /// Max length of free-text notes/descriptions
    /// </summary>
    public const int MaxNoteLength = 280;

    /// <summary>
    /// Window in which an already-rotated refresh token is still accepted (concurrent refreshes)
    /// </summary>
    public static readonly TimeSpan RefreshTokenReuseGrace = TimeSpan.FromSeconds(30);
}
