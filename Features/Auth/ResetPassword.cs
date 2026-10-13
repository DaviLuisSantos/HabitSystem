using HabitSystem.Common;
using HabitSystem.Infrastructure;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HabitSystem.Features.Auth;

public static class ResetPassword
{
    public record Command(string? Token, string? NewPassword) : IRequest<Result>;

    public class Handler : IRequestHandler<Command, Result>
    {
        private readonly AppDbContext _db;
        private readonly AuthService _auth;

        public Handler(AppDbContext db, AuthService auth)
        {
            _db = db;
            _auth = auth;
        }

        public async Task<Result> Handle(Command request, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(request.NewPassword) || request.NewPassword.Length < Constants.MinPasswordLength)
                return Result.Failure($"Password must be at least {Constants.MinPasswordLength} characters");

            if (string.IsNullOrWhiteSpace(request.Token))
                return Result.Failure("Invalid or expired reset token", ErrorCodes.InvalidToken);

            var user = await _db.Users
                .FirstOrDefaultAsync(u =>
                    u.PasswordResetToken == request.Token &&
                    u.PasswordResetTokenExpiry > DateTime.UtcNow, ct);

            if (user == null)
                return Result.Failure("Invalid or expired reset token", ErrorCodes.InvalidToken);

            user.PasswordHash = _auth.HashPassword(request.NewPassword);
            user.PasswordResetToken = null;
            user.PasswordResetTokenExpiry = null;

            // Token is single-use (cleared above); invalidate all existing sessions
            await _db.SaveChangesAsync(ct);
            await AuthService.RevokeAllRefreshTokensAsync(_db, user.Id, ct);
            return Result.Success();
        }
    }

    public static void MapResetPasswordEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/auth/reset-password", async (Command command, IMediator mediator) =>
        {
            var result = await mediator.Send(command);
            return result.IsSuccess
                ? Results.Ok(new { message = "Password updated successfully" })
                : result.ToErrorResult();
        })
        .WithName("ResetPassword")
        .WithOpenApi()
        .AllowAnonymous()
        .RequireRateLimiting("auth");
    }
}
