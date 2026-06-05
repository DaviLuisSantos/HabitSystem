namespace HabitSystem.Common;

/// <summary>
/// Application-wide constants
/// </summary>
public static class Constants
{
    /// <summary>
    /// Maximum number of active habits for free plan users
    /// </summary>
    public const int FreePlanHabitLimit = 5;

    /// <summary>
    /// Password reset token validity in hours
    /// </summary>
    public const int PasswordResetTokenExpiryHours = 2;

    /// <summary>
    /// Email verification token validity in hours
    /// </summary>
    public const int EmailVerificationTokenExpiryHours = 24;
}
