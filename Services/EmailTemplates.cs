using System.Net;

namespace HabitSystem.Services;

/// <summary>
/// Builds the links sent by e-mail (AppBaseUrl + frontend route + token)
/// </summary>
public static class EmailLinks
{
    public static string BaseUrl(IConfiguration configuration)
        => (configuration["AppBaseUrl"] is { Length: > 0 } url ? url : "http://localhost:3000").TrimEnd('/');

    public static string PasswordReset(IConfiguration configuration, string token)
        => $"{BaseUrl(configuration)}/reset-password?token={Uri.EscapeDataString(token)}";

    public static string EmailVerification(IConfiguration configuration, string token)
        => $"{BaseUrl(configuration)}/verify-email?token={Uri.EscapeDataString(token)}";
}

/// <summary>
/// Simple pt-BR HTML templates for transactional e-mails
/// </summary>
public static class EmailTemplates
{
    private const string ProductName = "Arco";

    public static (string Subject, string Html) PasswordReset(string name, string link) => (
        $"{ProductName} — Redefinição de senha",
        Layout(
            $"Olá, {WebUtility.HtmlEncode(name)}!",
            "Recebemos um pedido para redefinir a senha da sua conta no Arco. " +
            $"Clique no botão abaixo para escolher uma nova senha. O link expira em {HabitSystem.Common.Constants.PasswordResetTokenExpiryHours} horas.",
            "Redefinir senha",
            link,
            "Se você não pediu a redefinição, ignore este e-mail — sua senha continua a mesma."));

    public static (string Subject, string Html) EmailVerification(string name, string link) => (
        $"{ProductName} — Confirme seu e-mail",
        Layout(
            $"Bem-vindo(a) ao Arco, {WebUtility.HtmlEncode(name)}!",
            "Confirme seu endereço de e-mail clicando no botão abaixo. " +
            $"O link expira em {HabitSystem.Common.Constants.EmailVerificationTokenExpiryHours} horas.",
            "Confirmar e-mail",
            link,
            "Se você não criou uma conta no Arco, ignore este e-mail."));

    private static string Layout(string title, string body, string buttonText, string link, string footer)
    {
        var safeLink = WebUtility.HtmlEncode(link);
        return $"""
            <!DOCTYPE html>
            <html lang="pt-BR">
            <body style="margin:0;padding:24px;background:#f4f4f5;font-family:Arial,Helvetica,sans-serif;color:#18181b;">
              <table role="presentation" width="100%" cellspacing="0" cellpadding="0" style="max-width:520px;margin:0 auto;background:#ffffff;border-radius:12px;padding:32px;">
                <tr><td>
                  <h1 style="font-size:20px;margin:0 0 16px;">{title}</h1>
                  <p style="font-size:15px;line-height:1.6;margin:0 0 24px;">{body}</p>
                  <p style="margin:0 0 24px;">
                    <a href="{safeLink}" style="display:inline-block;background:#18181b;color:#ffffff;text-decoration:none;padding:12px 20px;border-radius:8px;font-weight:bold;">{buttonText}</a>
                  </p>
                  <p style="font-size:13px;line-height:1.5;color:#71717a;margin:0 0 8px;">Se o botão não funcionar, copie e cole este link no navegador:</p>
                  <p style="font-size:13px;word-break:break-all;margin:0 0 24px;"><a href="{safeLink}" style="color:#2563eb;">{safeLink}</a></p>
                  <p style="font-size:13px;color:#71717a;margin:0;">{footer}</p>
                </td></tr>
              </table>
              <p style="text-align:center;font-size:12px;color:#a1a1aa;margin-top:16px;">{ProductName}</p>
            </body>
            </html>
            """;
    }
}
