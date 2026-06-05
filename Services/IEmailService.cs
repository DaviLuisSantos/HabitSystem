namespace HabitSystem.Services;

public interface IEmailService
{
    Task SendPasswordResetAsync(string toEmail, string toName, string token, CancellationToken ct = default);
    Task SendEmailVerificationAsync(string toEmail, string toName, string token, CancellationToken ct = default);
}
