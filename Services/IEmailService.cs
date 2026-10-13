namespace HabitSystem.Services;

/// <summary>
/// Sends transactional e-mails.
/// Implementations must be fail-safe: log failures (never the token/link) and NOT throw,
/// so that a provider outage never breaks register / forgot-password / resend flows.
/// </summary>
public interface IEmailService
{
    Task SendPasswordResetAsync(string toEmail, string toName, string token, CancellationToken ct = default);
    Task SendEmailVerificationAsync(string toEmail, string toName, string token, CancellationToken ct = default);
}
