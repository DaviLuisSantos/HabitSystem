using HabitSystem.Common;
using HabitSystem.Domain;
using HabitSystem.Infrastructure;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HabitSystem.Features.Auth;

/// <summary>
/// Register endpoint for creating new user accounts
/// </summary>
public static class Register
{
    public record Command(string Name, string Email, string Password, string? Timezone = null) : IRequest<Result<Response>>;

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

            if (request.Password.Length < 6)
                return Result<Response>.Failure("Password must be at least 6 characters");

            if (string.IsNullOrWhiteSpace(request.Name))
                return Result<Response>.Failure("Name is required");

            // Check if email already exists
            var emailExists = await _context.Users
                .AnyAsync(u => u.Email.ToLower() == request.Email.ToLower(), cancellationToken);

            if (emailExists)
                return Result<Response>.Failure("Email already registered");

            // Create new user
            var user = new User
            {
                Id = Guid.NewGuid(),
                Name = request.Name,
                Email = request.Email.ToLower(),
                Timezone = request.Timezone ?? "America/Sao_Paulo",
                PasswordHash = _authService.HashPassword(request.Password),
                RefreshToken = _authService.GenerateRefreshToken(),
                RefreshTokenExpiryTime = _authService.GetRefreshTokenExpiryTime(),
                CreatedAt = DateTime.UtcNow
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync(cancellationToken);

            // Generate tokens
            var accessToken = _authService.GenerateAccessToken(user);

            return Result<Response>.Success(new Response(
                user.Id,
                user.Email,
                user.Name,
                accessToken,
                user.RefreshToken!
            ));
        }
    }

    public static void MapRegisterEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/auth/register", async (Command command, IMediator mediator) =>
        {
            var result = await mediator.Send(command);
            return result.IsSuccess
                ? Results.Ok(result.Value)
                : Results.BadRequest(new { error = result.Error });
        })
        .WithName("Register")
        .WithOpenApi()
        .AllowAnonymous();
    }
}
