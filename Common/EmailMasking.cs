namespace HabitSystem.Common;

/// <summary>
/// Helpers to avoid writing full e-mail addresses to logs
/// </summary>
public static class EmailMasking
{
    /// <summary>
    /// "john.doe@example.com" becomes "j***@example.com"
    /// </summary>
    public static string Mask(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
            return "(empty)";

        var at = email.IndexOf('@');
        if (at <= 0)
            return "***";

        return $"{email[0]}***{email[at..]}";
    }
}
