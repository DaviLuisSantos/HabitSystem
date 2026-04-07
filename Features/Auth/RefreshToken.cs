using HabitSystem.Common;
using HabitSystem.Infrastructure;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HabitSystem.Features.Auth;

/// <summary>
/// Refresh token endpoint for renewing access tokens
/// </summary>
public static class RefreshToken
{
    public record Command(string AccessToken, string RefreshToken) : IRequest<Result<Response>>;

    public record Response(string AccessToken, string RefreshToken);

    public class Handler : IRequestHandler<Command, Result<Response>>
    {
        private readonly AppDbContext _context;
        private readonly AuthService _authService;

        public Handler(AppDbContext context, AuthService authService)
        {
            _context = context;
            _authService = authService;
        }

        public async Task<Result<Response>> Handle(Command request, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(request.AccessToken) || string.IsNullOrWhiteSpace(request.RefreshToken))
                return Result<Response>.Failure("Access token and refresh token are required");

            // Get principal from expired token
            var principal = _authService.GetPrincipalFromToken(request.AccessToken);
            if (principal == null)
                return Result<Response>.Failure("Invalid access token");

            // Get user ID from claims
            var userIdClaim = principal.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
                return Result<Response>.Failure("Invalid token claims");

            // Find user
            var user = await _context.Users.FindAsync(new object[] { userId }, cancellationToken);
            if (user == null)
                return Result<Response>.Failure("User not found");

            // Validate refresh token
            if (user.RefreshToken != request.RefreshToken)
                return Result<Response>.Failure("Invalid refresh token");

            if (user.RefreshTokenExpiryTime == null || user.RefreshTokenExpiryTime <= DateTime.UtcNow)
                return Result<Response>.Failure("Refresh token expired");

            // Generate new tokens
            var newAccessToken = _authService.GenerateAccessToken(user);
            var newRefreshToken = _authService.GenerateRefreshToken();

            // Update refresh token in database
            user.RefreshToken = newRefreshToken;
            user.RefreshTokenExpiryTime = _authService.GetRefreshTokenExpiryTime();
            await _context.SaveChangesAsync(cancellationToken);

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
                : Results.BadRequest(new { error = result.Error });
        })
        .WithName("RefreshToken")
        .WithOpenApi()
        .AllowAnonymous();
    }
}
