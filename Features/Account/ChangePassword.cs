using HabitSystem.Common;
using HabitSystem.Features.Auth;
using HabitSystem.Infrastructure;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace HabitSystem.Features.Account;

public static class ChangePassword
{
    public record Command(string CurrentPassword, string NewPassword) : IRequest<Result>;

    public class Handler : IRequestHandler<Command, Result>
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

        public async Task<Result> Handle(Command request, CancellationToken ct)
        {
            var userId = _http.HttpContext?.User.GetUserId();
            if (userId == null) return Result.Failure("Not authenticated");

            if (string.IsNullOrWhiteSpace(request.NewPassword) || request.NewPassword.Length < 6)
                return Result.Failure("New password must be at least 6 characters");

            var user = await _db.Users.FindAsync(new object[] { userId.Value }, ct);
            if (user == null) return Result.Failure("User not found");

            if (!_auth.VerifyPassword(request.CurrentPassword, user.PasswordHash))
                return Result.Failure("Current password is incorrect");

            user.PasswordHash = _auth.HashPassword(request.NewPassword);
            await _db.SaveChangesAsync(ct);
            return Result.Success();
        }
    }

    public static void MapChangePasswordEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapPut("/api/account/password", [Authorize] async (Command command, IMediator mediator) =>
        {
            var result = await mediator.Send(command);
            return result.IsSuccess
                ? Results.Ok(new { message = "Password updated" })
                : Results.BadRequest(new { error = result.Error });
        })
        .WithName("ChangePassword")
        .WithOpenApi()
        .RequireAuthorization();
    }
}
