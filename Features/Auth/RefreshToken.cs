using HabitSystem.Common;
using HabitSystem.Infrastructure;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HabitSystem.Features.Auth;

/// <summary>
/// Refresh token endpoint for renewing access tokens.
/// Tokens are rotated on every use (old one revoked, new one issued). A rotated token
/// presented again within <see cref="Constants.RefreshTokenReuseGrace"/> still yields a fresh
/// pair (concurrent refresh from several tabs); after that it is treated as token theft and
/// every refresh token of the user is revoked.
/// </summary>
public static class RefreshToken
{
    /// <summary>
    /// AccessToken is optional (kept for contract compatibility). When sent, it must be
    /// a token signed by us for the same user as the refresh token.
    /// </summary>
    public record Command(string? AccessToken, string RefreshToken) : IRequest<Result<Response>>;

    public record Response(string AccessToken, string RefreshToken);

    public class Handler : IRequestHandler<Command, Result<Response>>
    {
        private readonly AppDbContext _context;
        private readonly AuthService _authService;
        private readonly ILogger<Handler> _logger;

        public Handler(AppDbContext context, AuthService authService, ILogger<Handler> logger)
        {
            _context = context;
            _authService = authService;
            _logger = logger;
        }

        public async Task<Result<Response>> Handle(Command request, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(request.RefreshToken))
                return Result<Response>.Failure("Refresh token is required", ErrorCodes.InvalidRefreshToken);

            var hash = AuthService.HashToken(request.RefreshToken);
            var stored = await _context.RefreshTokens
                .Include(t => t.User)
                .FirstOrDefaultAsync(t => t.TokenHash == hash, cancellationToken);

            if (stored == null)
                return Result<Response>.Failure("Invalid refresh token", ErrorCodes.InvalidRefreshToken);

            // Optional cross-check with the (possibly expired) access token
            if (!string.IsNullOrWhiteSpace(request.AccessToken))
            {
                var principal = _authService.GetPrincipalFromToken(request.AccessToken);
                if (principal?.GetUserId() != stored.UserId)
                    return Result<Response>.Failure("Invalid access token", ErrorCodes.InvalidRefreshToken);
            }

            var now = DateTime.UtcNow;

            if (stored.ExpiresAt <= now)
                return Result<Response>.Failure("Refresh token expired", ErrorCodes.InvalidRefreshToken);

            if (stored.RevokedAt != null)
            {
                var rotated = stored.ReplacedByTokenHash != null;
                var withinGrace = rotated && now - stored.RevokedAt.Value <= Constants.RefreshTokenReuseGrace;

                if (!withinGrace)
                {
                    if (rotated)
                    {
                        // Reuse of a rotated token outside the grace window: assume theft
                        _logger.LogWarning("Refresh token reuse detected for user {UserId}; revoking all sessions.", stored.UserId);
                        await AuthService.RevokeAllRefreshTokensAsync(_context, stored.UserId, cancellationToken);
                    }

                    return Result<Response>.Failure("Invalid refresh token", ErrorCodes.InvalidRefreshToken);
                }

                // Benign concurrent refresh: issue another fresh pair, keep the previous replacement valid
                var graceToken = _authService.IssueRefreshToken(_context, stored.UserId);
                await _context.SaveChangesAsync(cancellationToken);
                return Result<Response>.Success(new Response(_authService.GenerateAccessToken(stored.User), graceToken));
            }

            // Normal rotation
            var newRefreshToken = _authService.IssueRefreshToken(_context, stored.UserId);
            stored.RevokedAt = now;
            stored.ReplacedByTokenHash = AuthService.HashToken(newRefreshToken);
            await _context.SaveChangesAsync(cancellationToken);

            var newAccessToken = _authService.GenerateAccessToken(stored.User);
            return Result<Response>.Success(new Response(newAccessToken, newRefreshToken));
        }
    }

    public static void MapRefreshTokenEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/auth/refresh", async (Command command, IMediator mediator) =>
        {
            var result = await mediator.Send(command);
            return result.IsSuccess
                ? Results.Ok(result.Value)
                : result.ToErrorResult();
        })
        .WithName("RefreshToken")
        .WithOpenApi()
        .AllowAnonymous()
        .RequireRateLimiting("auth");
    }
}
