using HabitSystem.Common;
using HabitSystem.Infrastructure;
using HabitSystem.Services;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;

namespace HabitSystem.Features.Auth;

public static class VerifyEmail
{
    public record VerifyCommand(string? Token) : IRequest<Result>;

    /// <summary>
    /// Resends the verification e-mail to the authenticated user (id taken from the JWT claims)
    /// </summary>
    public record ResendCommand(Guid UserId) : IRequest<Result<bool>>;

    // ── Verify handler ───────────────────────────────────────────────────────

    public class VerifyHandler : IRequestHandler<VerifyCommand, Result>
    {
        private readonly AppDbContext _db;

        public VerifyHandler(AppDbContext db) => _db = db;

        public async Task<Result> Handle(VerifyCommand request, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(request.Token))
                return Result.Failure("Invalid or expired verification token", ErrorCodes.InvalidToken);

            var user = await _db.Users.FirstOrDefaultAsync(u =>
                u.EmailVerificationToken == request.Token &&
                u.EmailVerificationTokenExpiry > DateTime.UtcNow, ct);

            if (user == null)
                return Result.Failure("Invalid or expired verification token", ErrorCodes.InvalidToken);

            // Single-use: token is cleared once consumed
            user.IsEmailVerified = true;
            user.EmailVerificationToken = null;
            user.EmailVerificationTokenExpiry = null;

            await _db.SaveChangesAsync(ct);
            return Result.Success();
        }
    }

    // ── Resend handler ───────────────────────────────────────────────────────

    /// <summary>
    /// Returns true when an e-mail was sent, false when the user was already verified (no-op)
    /// </summary>
    public class ResendHandler : IRequestHandler<ResendCommand, Result<bool>>
    {
        private readonly AppDbContext _db;
        private readonly IEmailService _email;

        public ResendHandler(AppDbContext db, IEmailService email)
        {
            _db = db;
            _email = email;
        }

        public async Task<Result<bool>> Handle(ResendCommand request, CancellationToken ct)
        {
            var user = await _db.Users.FindAsync(new object[] { request.UserId }, ct);
            if (user == null)
                return Result<bool>.Failure("User not found", ErrorCodes.Unauthorized);

            if (user.IsEmailVerified)
                return Result<bool>.Success(false);

            var token = GenerateToken();
            user.EmailVerificationToken = token;
            user.EmailVerificationTokenExpiry = DateTime.UtcNow
                .AddHours(Constants.EmailVerificationTokenExpiryHours);

            await _db.SaveChangesAsync(ct);
            // Fail-safe: e-mail errors are logged by the service and never surface here
            await _email.SendEmailVerificationAsync(user.Email, user.Name, token, ct);

            return Result<bool>.Success(true);
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
                : result.ToErrorResult();
        })
        .WithName("VerifyEmail")
        .WithOpenApi()
        .AllowAnonymous()
        .RequireRateLimiting("auth");

        // Body is ignored (may be empty); the user comes from the access token
        app.MapPost("/api/auth/resend-verification", [Authorize] async (HttpContext httpContext, IMediator mediator) =>
        {
            var userId = httpContext.User.GetUserId();
            if (userId == null)
                return ResultHttpExtensions.ToErrorResult("User not authenticated", ErrorCodes.Unauthorized);

            var result = await mediator.Send(new ResendCommand(userId.Value));
            if (!result.IsSuccess)
                return result.ToErrorResult();

            return result.Value
                ? Results.Ok(new { message = "Verification email sent." })
                : Results.Ok(new { message = "Email already verified." });
        })
        .WithName("ResendVerification")
        .WithOpenApi()
        .RequireAuthorization()
        .RequireRateLimiting("auth");
    }
}
