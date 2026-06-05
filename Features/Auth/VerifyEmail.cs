using HabitSystem.Common;
using HabitSystem.Infrastructure;
using HabitSystem.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;

namespace HabitSystem.Features.Auth;

public static class VerifyEmail
{
    public record VerifyCommand(string Token) : IRequest<Result>;
    public record ResendCommand(string Email) : IRequest<Result>;

    // ── Verify handler ───────────────────────────────────────────────────────

    public class VerifyHandler : IRequestHandler<VerifyCommand, Result>
    {
        private readonly AppDbContext _db;

        public VerifyHandler(AppDbContext db) => _db = db;

        public async Task<Result> Handle(VerifyCommand request, CancellationToken ct)
        {
            var user = await _db.Users.FirstOrDefaultAsync(u =>
                u.EmailVerificationToken == request.Token &&
                u.EmailVerificationTokenExpiry > DateTime.UtcNow, ct);

            if (user == null)
                return Result.Failure("Invalid or expired verification token");

            user.IsEmailVerified = true;
            user.EmailVerificationToken = null;
            user.EmailVerificationTokenExpiry = null;

            await _db.SaveChangesAsync(ct);
            return Result.Success();
        }
    }

    // ── Resend handler ───────────────────────────────────────────────────────

    public class ResendHandler : IRequestHandler<ResendCommand, Result>
    {
        private readonly AppDbContext _db;
        private readonly IEmailService _email;

        public ResendHandler(AppDbContext db, IEmailService email)
        {
            _db = db;
            _email = email;
        }

        public async Task<Result> Handle(ResendCommand request, CancellationToken ct)
        {
            var user = await _db.Users
                .FirstOrDefaultAsync(u => u.Email == request.Email.ToLower(), ct);

            if (user == null || user.IsEmailVerified)
                return Result.Success(); // Don't reveal info

            var token = GenerateToken();
            user.EmailVerificationToken = token;
            user.EmailVerificationTokenExpiry = DateTime.UtcNow
                .AddHours(Constants.EmailVerificationTokenExpiryHours);

            await _db.SaveChangesAsync(ct);
            await _email.SendEmailVerificationAsync(user.Email, user.Name, token, ct);

            return Result.Success();
        }
    }

    private static string GenerateToken()
    {
        var bytes = new byte[32];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToBase64String(bytes)
            .Replace("+", "-").Replace("/", "_").Replace("=", "");
    }

    // ── Endpoints ────────────────────────────────────────────────────────────

    public static void MapVerifyEmailEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/auth/verify-email", async (VerifyCommand command, IMediator mediator) =>
        {
            var result = await mediator.Send(command);
            return result.IsSuccess
                ? Results.Ok(new { message = "Email verified successfully" })
                : Results.BadRequest(new { error = result.Error });
        })
        .WithName("VerifyEmail")
        .WithOpenApi()
        .AllowAnonymous();

        app.MapPost("/api/auth/resend-verification", async (ResendCommand command, IMediator mediator) =>
        {
            await mediator.Send(command);
            return Results.Ok(new { message = "If this email is registered and unverified, a new link was sent." });
        })
        .WithName("ResendVerification")
        .WithOpenApi()
        .AllowAnonymous();
    }
}
