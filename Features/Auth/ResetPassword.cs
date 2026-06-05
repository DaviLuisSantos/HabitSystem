using HabitSystem.Common;
using HabitSystem.Infrastructure;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HabitSystem.Features.Auth;

public static class ResetPassword
{
    public record Command(string Token, string NewPassword) : IRequest<Result>;

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
            if (string.IsNullOrWhiteSpace(request.NewPassword) || request.NewPassword.Length < 6)
                return Result.Failure("Password must be at least 6 characters");

            var user = await _db.Users
                .FirstOrDefaultAsync(u =>
                    u.PasswordResetToken == request.Token &&
                    u.PasswordResetTokenExpiry > DateTime.UtcNow, ct);

            if (user == null)
                return Result.Failure("Invalid or expired reset token");

            user.PasswordHash = _auth.HashPassword(request.NewPassword);
            user.PasswordResetToken = null;
            user.PasswordResetTokenExpiry = null;
            // Invalidate all existing sessions
            user.RefreshToken = null;
            user.RefreshTokenExpiryTime = null;

            await _db.SaveChangesAsync(ct);
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
                : Results.BadRequest(new { error = result.Error });
        })
        .WithName("ResetPassword")
        .WithOpenApi()
        .AllowAnonymous();
    }
}
