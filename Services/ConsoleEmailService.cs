namespace HabitSystem.Services;

/// <summary>
/// Stub implementation: logs email content to console.
/// Replace with a real provider (SendGrid, Resend, SMTP) setting
/// EMAIL_PROVIDER env var and registering the concrete implementation.
/// </summary>
public class ConsoleEmailService : IEmailService
{
    private readonly ILogger<ConsoleEmailService> _logger;
    private readonly IConfiguration _configuration;

    public ConsoleEmailService(ILogger<ConsoleEmailService> logger, IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
    }

    public Task SendPasswordResetAsync(string toEmail, string toName, string token, CancellationToken ct = default)
    {
        var baseUrl = _configuration["AppBaseUrl"] ?? "http://localhost:3000";
        var link = $"{baseUrl}/reset-password?token={token}";

        _logger.LogInformation(
            "[EMAIL] Password reset for {Email} — link: {Link}",
            toEmail, link);

        return Task.CompletedTask;
    }

    public Task SendEmailVerificationAsync(string toEmail, string toName, string token, CancellationToken ct = default)
    {
        var baseUrl = _configuration["AppBaseUrl"] ?? "http://localhost:3000";
        var link = $"{baseUrl}/verify-email?token={token}";

        _logger.LogInformation(
            "[EMAIL] Verify email for {Email} — link: {Link}",
            toEmail, link);

        return Task.CompletedTask;
    }
}
