using System.Net.Mail;
using System.Security.Cryptography;
using HabitSystem.Common;
using HabitSystem.Domain;
using HabitSystem.Infrastructure;
using HabitSystem.Services;
using MediatR;
using Microsoft.Data.Sqlite;
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
        private readonly IEmailService _email;

        public Handler(AppDbContext context, AuthService authService, IEmailService email)
        {
            _context = context;
            _authService = authService;
            _email = email;
        }

        public async Task<Result<Response>> Handle(Command request, CancellationToken cancellationToken)
        {
            // Validate input
            var name = request.Name?.Trim();
            var email = request.Email?.Trim().ToLowerInvariant();

            if (string.IsNullOrWhiteSpace(email))
                return Result<Response>.Failure("Email is required");

            if (email.Length > 255 || !IsValidEmail(email))
                return Result<Response>.Failure("Invalid email format");

            if (string.IsNullOrWhiteSpace(request.Password))
                return Result<Response>.Failure("Password is required");

            if (request.Password.Length < Constants.MinPasswordLength)
                return Result<Response>.Failure($"Password must be at least {Constants.MinPasswordLength} characters");

            if (string.IsNullOrWhiteSpace(name))
                return Result<Response>.Failure("Name is required");

            if (name.Length > 100)
                return Result<Response>.Failure("Name must be at most 100 characters");

            // Check if email already exists
            var emailExists = await _context.Users
                .AnyAsync(u => u.Email.ToLower() == email, cancellationToken);

            if (emailExists)
                return Result<Response>.Failure("Email already registered", ErrorCodes.EmailTaken);

            // Unknown / invalid timezone ids fall back to the default
            var timezone = request.Timezone?.Trim();
            if (timezone == null || timezone.Length > 50 || !UserClock.IsValidTimeZone(timezone))
                timezone = UserClock.DefaultTimezone;

            var verificationToken = GenerateToken();

            // Create new user
            var user = new User
            {
                Id = Guid.NewGuid(),
                Name = name,
                Email = email,
                Timezone = timezone,
                PasswordHash = _authService.HashPassword(request.Password),
                EmailVerificationToken = verificationToken,
                EmailVerificationTokenExpiry = DateTime.UtcNow.AddHours(Constants.EmailVerificationTokenExpiryHours),
                CreatedAt = DateTime.UtcNow
            };

            _context.Users.Add(user);
            var refreshToken = _authService.IssueRefreshToken(_context, user.Id);

            try
            {
                await _context.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException ex) when (ex.InnerException is SqliteException { SqliteErrorCode: 19 })
            {
                // Concurrent registration with the same e-mail hit the unique index
                return Result<Response>.Failure("Email already registered", ErrorCodes.EmailTaken);
            }

            // Verification is optional and must never block registration (service is fail-safe)
            await _email.SendEmailVerificationAsync(user.Email, user.Name, verificationToken, cancellationToken);

            // Generate tokens
            var accessToken = _authService.GenerateAccessToken(user);

            return Result<Response>.Success(new Response(
                user.Id,
                user.Email,
                user.Name,
                accessToken,
                refreshToken
            ));
        }

        private static bool IsValidEmail(string email)
        {
            try
            {
                var address = new MailAddress(email);
                return address.Address == email && address.Host.Contains('.', StringComparison.Ordinal);
            }
            catch (FormatException)
            {
                return false;
            }
        }

        private static string GenerateToken()
        {
            var bytes = new byte[32];
            RandomNumberGenerator.Fill(bytes);
            return Convert.ToBase64String(bytes)
                .Replace("+", "-").Replace("/", "_").Replace("=", "");
        }
    }

    public static void MapRegisterEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/auth/register", async (Command command, IMediator mediator) =>
        {
            var result = await mediator.Send(command);
            return result.IsSuccess
                ? Results.Ok(result.Value)
                : result.ToErrorResult();
        })
        .WithName("Register")
        .WithOpenApi()
        .AllowAnonymous()
        .RequireRateLimiting("auth");
    }
}
