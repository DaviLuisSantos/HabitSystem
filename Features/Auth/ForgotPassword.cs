using HabitSystem.Common;
using HabitSystem.Infrastructure;
using HabitSystem.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;

namespace HabitSystem.Features.Auth;

public static class ForgotPassword
{
    public record Command(string Email) : IRequest<Result>;

    public class Handler : IRequestHandler<Command, Result>
    {
        private readonly AppDbContext _db;
        private readonly IEmailService _email;

        public Handler(AppDbContext db, IEmailService email)
        {
            _db = db;
            _email = email;
        }

        public async Task<Result> Handle(Command request, CancellationToken ct)
        {
            // Always return success — don't reveal whether email exists
            var user = await _db.Users
                .FirstOrDefaultAsync(u => u.Email == request.Email.ToLower(), ct);

            if (user != null)
            {
                var token = GenerateToken();
                user.PasswordResetToken = token;
                user.PasswordResetTokenExpiry = DateTime.UtcNow
                    .AddHours(Constants.PasswordResetTokenExpiryHours);

                await _db.SaveChangesAsync(ct);
                await _email.SendPasswordResetAsync(user.Email, user.Name, token, ct);
            }

            return Result.Success();
        }

        private static string GenerateToken()
        {
            var bytes = new byte[32];
            RandomNumberGenerator.Fill(bytes);
            return Convert.ToBase64String(bytes)
                .Replace("+", "-").Replace("/", "_").Replace("=", "");
        }
    }

    public static void MapForgotPasswordEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/auth/forgot-password", async (Command command, IMediator mediator) =>
        {
            await mediator.Send(command);
            // Sempre 200 para não revelar se o e-mail existe
            return Results.Ok(new { message = "If this email is registered, a reset link was sent." });
        })
        .WithName("ForgotPassword")
        .WithOpenApi()
        .AllowAnonymous()
        .RequireRateLimiting("auth");
    }
}
