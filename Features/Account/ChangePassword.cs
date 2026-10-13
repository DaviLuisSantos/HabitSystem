using HabitSystem.Common;
using HabitSystem.Features.Auth;
using HabitSystem.Infrastructure;
using MediatR;
using Microsoft.AspNetCore.Authorization;

namespace HabitSystem.Features.Account;

public static class ChangePassword
{
    public record Command(string? CurrentPassword, string? NewPassword) : IRequest<Result<Response>>;

    /// <summary>
    /// All previous refresh tokens (every device) are revoked; a fresh pair is returned
    /// so the current session keeps working.
    /// </summary>
    public record Response(string Message, string AccessToken, string RefreshToken);

    public class Handler : IRequestHandler<Command, Result<Response>>
    {
        private readonly AppDbContext _db;
        private readonly AuthService _auth;
        private readonly IHttpContextAccessor _http;

        public Handler(AppDbContext db, AuthService auth, IHttpContextAccessor http)
        {
            _db = db;
            _auth = auth;
            _http = http;
        }

        public async Task<Result<Response>> Handle(Command request, CancellationToken ct)
        {
            var userId = _http.HttpContext?.User.GetUserId();
            if (userId == null) return Result<Response>.Failure("Not authenticated", ErrorCodes.Unauthorized);

            if (string.IsNullOrWhiteSpace(request.NewPassword) || request.NewPassword.Length < Constants.MinPasswordLength)
                return Result<Response>.Failure($"New password must be at least {Constants.MinPasswordLength} characters");

            var user = await _db.Users.FindAsync(new object[] { userId.Value }, ct);
            if (user == null) return Result<Response>.Failure("User not found", ErrorCodes.Unauthorized);

            if (string.IsNullOrEmpty(request.CurrentPassword) || !_auth.VerifyPassword(request.CurrentPassword, user.PasswordHash))
                return Result<Response>.Failure("Current password is incorrect", ErrorCodes.InvalidPassword);

            user.PasswordHash = _auth.HashPassword(request.NewPassword);

            // Revoke every session, then issue a new one for the caller
            await AuthService.RevokeAllRefreshTokensAsync(_db, user.Id, ct);
            var refreshToken = _auth.IssueRefreshToken(_db, user.Id);
            await _db.SaveChangesAsync(ct);

            return Result<Response>.Success(new Response(
                "Password updated",
                _auth.GenerateAccessToken(user),
                refreshToken));
        }
    }

    public static void MapChangePasswordEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapPut("/api/account/password", [Authorize] async (Command command, IMediator mediator) =>
        {
            var result = await mediator.Send(command);
            return result.IsSuccess
                ? Results.Ok(result.Value)
                : result.ToErrorResult();
        })
        .WithName("ChangePassword")
        .WithOpenApi()
        .RequireAuthorization();
    }
}
