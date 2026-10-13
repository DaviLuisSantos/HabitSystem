using HabitSystem.Common;
using HabitSystem.Infrastructure;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HabitSystem.Features.Auth;

/// <summary>
/// Login endpoint for user authentication
/// </summary>
public static class Login
{
    public record Command(string Email, string Password) : IRequest<Result<Response>>;

    public record Response(Guid UserId, string Email, string Name, string AccessToken, string RefreshToken);

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
            // Validate input
            if (string.IsNullOrWhiteSpace(request.Email))
                return Result<Response>.Failure("Email is required");

            if (string.IsNullOrWhiteSpace(request.Password))
                return Result<Response>.Failure("Password is required");

            // Find user by email
            var email = request.Email.Trim().ToLowerInvariant();
            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.Email.ToLower() == email, cancellationToken);

            if (user == null)
                return Result<Response>.Failure("Invalid email or password", ErrorCodes.InvalidCredentials);

            // Verify password
            if (!_authService.VerifyPassword(request.Password, user.PasswordHash))
                return Result<Response>.Failure("Invalid email or password", ErrorCodes.InvalidCredentials);

            // Generate new tokens (one refresh token per device/session)
            var accessToken = _authService.GenerateAccessToken(user);
            var refreshToken = _authService.IssueRefreshToken(_context, user.Id);
            await _context.SaveChangesAsync(cancellationToken);

            return Result<Response>.Success(new Response(
                user.Id,
                user.Email,
                user.Name,
                accessToken,
                refreshToken
            ));
        }
    }

    public static void MapLoginEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/auth/login", async (Command command, IMediator mediator) =>
        {
            var result = await mediator.Send(command);
            return result.IsSuccess
                ? Results.Ok(result.Value)
                : result.ToErrorResult();
        })
        .WithName("Login")
        .WithOpenApi()
        .AllowAnonymous()
        .RequireRateLimiting("auth");
    }
}
