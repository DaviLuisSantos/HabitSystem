using HabitSystem.Common;

namespace HabitSystem.Services;

/// <summary>
/// Fallback implementation used when Email:ResendApiKey is not configured.
/// In Development it logs the link (to ease local testing); in any other
/// environment it never logs the token/link and masks the recipient.
/// </summary>
public class ConsoleEmailService : IEmailService
{
    private readonly ILogger<ConsoleEmailService> _logger;
    private readonly IConfiguration _configuration;
    private readonly IHostEnvironment _environment;

    public ConsoleEmailService(ILogger<ConsoleEmailService> logger, IConfiguration configuration, IHostEnvironment environment)
    {
        _logger = logger;
        _configuration = configuration;
        _environment = environment;
    }

    public Task SendPasswordResetAsync(string toEmail, string toName, string token, CancellationToken ct = default)
    {
        Log("Password reset", toEmail, EmailLinks.PasswordReset(_configuration, token));
        return Task.CompletedTask;
    }

    public Task SendEmailVerificationAsync(string toEmail, string toName, string token, CancellationToken ct = default)
    {
        Log("Verify email", toEmail, EmailLinks.EmailVerification(_configuration, token));
        return Task.CompletedTask;
    }

    private void Log(string kind, string toEmail, string link)
    {
        if (_environment.IsDevelopment())
        {
            _logger.LogInformation("[EMAIL] {Kind} for {Email} — link: {Link}", kind, toEmail, link);
        }
        else
        {
            _logger.LogWarning(
                "[EMAIL] {Kind} for {Email} NOT sent: no e-mail provider configured (set Email__ResendApiKey).",
                kind, EmailMasking.Mask(toEmail));
        }
    }
}
