using HabitSystem.Common;
using HabitSystem.Infrastructure;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HabitSystem.Features.Auth;

/// <summary>
/// Logout endpoint: revokes the given refresh token (this device only).
/// Idempotent — unknown or already-revoked tokens also return 200.
/// </summary>
public static class Logout
{
    public record Command(string? RefreshToken) : IRequest<Result>;

    public class Handler : IRequestHandler<Command, Result>
    {
        private readonly AppDbContext _context;

        public Handler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<Result> Handle(Command request, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(request.RefreshToken))
                return Result.Failure("Refresh token is required");

            var hash = AuthService.HashToken(request.RefreshToken);
            var stored = await _context.RefreshTokens
                .FirstOrDefaultAsync(t => t.TokenHash == hash, cancellationToken);

            if (stored != null && stored.RevokedAt == null)
            {
                stored.RevokedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync(cancellationToken);
            }

            return Result.Success();
        }
    }

    public static void MapLogoutEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/auth/logout", async (Command command, IMediator mediator) =>
        {
            var result = await mediator.Send(command);
            return result.IsSuccess
                ? Results.Ok(new { message = "Logged out" })
                : result.ToErrorResult();
        })
        .WithName("Logout")
        .WithOpenApi()
        .AllowAnonymous()
        .RequireRateLimiting("auth");
    }
}
