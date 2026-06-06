using HabitSystem.Common;
using HabitSystem.Features.Auth;
using HabitSystem.Infrastructure;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HabitSystem.Features.Account;

public static class DeleteAccount
{
    public record Command(string Password) : IRequest<Result>;

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

            var user = await _db.Users.FindAsync(new object[] { userId.Value }, ct);
            if (user == null) return Result.Failure("User not found");

            if (!_auth.VerifyPassword(request.Password, user.PasswordHash))
                return Result.Failure("Password is incorrect");

            // Cascade deletes handle related data (CheckIns, Habits, DailyScores)
            _db.Users.Remove(user);
            await _db.SaveChangesAsync(ct);
            return Result.Success();
        }
    }

    public static void MapDeleteAccountEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapDelete("/api/account", [Authorize] async ([FromBody] Command command, [FromServices] IMediator mediator) =>
        {
            var result = await mediator.Send(command);
            return result.IsSuccess
                ? Results.Ok(new { message = "Account deleted" })
                : Results.BadRequest(new { error = result.Error });
        })
        .WithName("DeleteAccount")
        .WithOpenApi()
        .RequireAuthorization();
    }
}
