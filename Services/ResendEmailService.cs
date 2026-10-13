using System.Net.Http.Headers;
using HabitSystem.Common;

namespace HabitSystem.Services;

/// <summary>
/// Sends e-mails through the Resend HTTP API (POST https://api.resend.com/emails).
/// Config: Email:ResendApiKey, Email:From, AppBaseUrl.
/// Fail-safe: errors are logged (without token/link) and never thrown.
/// </summary>
public class ResendEmailService : IEmailService
{
    public const string ApiBaseUrl = "https://api.resend.com/";

    private readonly HttpClient _http;
    private readonly IConfiguration _configuration;
    private readonly ILogger<ResendEmailService> _logger;

    public ResendEmailService(HttpClient http, IConfiguration configuration, ILogger<ResendEmailService> logger)
    {
        _http = http;
        _configuration = configuration;
        _logger = logger;
    }

    public Task SendPasswordResetAsync(string toEmail, string toName, string token, CancellationToken ct = default)
    {
        var (subject, html) = EmailTemplates.PasswordReset(toName, EmailLinks.PasswordReset(_configuration, token));
        return SendAsync("password_reset", toEmail, subject, html, ct);
    }

    public Task SendEmailVerificationAsync(string toEmail, string toName, string token, CancellationToken ct = default)
    {
        var (subject, html) = EmailTemplates.EmailVerification(toName, EmailLinks.EmailVerification(_configuration, token));
        return SendAsync("email_verification", toEmail, subject, html, ct);
    }

    private async Task SendAsync(string kind, string toEmail, string subject, string html, CancellationToken ct)
    {
        try
        {
            var apiKey = _configuration["Email:ResendApiKey"];
            var from = _configuration["Email:From"];
            if (string.IsNullOrWhiteSpace(from))
                from = "Arco <onboarding@resend.dev>";

            using var request = new HttpRequestMessage(HttpMethod.Post, "emails")
            {
                Content = JsonContent.Create(new
                {
                    from,
                    to = new[] { toEmail },
                    subject,
                    html
                })
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

            using var response = await _http.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(ct);
                if (body.Length > 500) body = body[..500];
                _logger.LogError(
                    "[EMAIL] Resend rejected {Kind} e-mail to {Email}: HTTP {Status} {Body}",
                    kind, EmailMasking.Mask(toEmail), (int)response.StatusCode, body);
                return;
            }

            _logger.LogInformation("[EMAIL] {Kind} e-mail sent to {Email}", kind, EmailMasking.Mask(toEmail));
        }
        catch (Exception ex)
        {
            // Exception messages from HttpClient never contain the request body (token/link)
            _logger.LogError(
                "[EMAIL] Failed to send {Kind} e-mail to {Email}: {ErrorType}: {ErrorMessage}",
                kind, EmailMasking.Mask(toEmail), ex.GetType().Name, ex.Message);
        }
    }
}
