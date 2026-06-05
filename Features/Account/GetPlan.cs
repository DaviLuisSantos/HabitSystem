using HabitSystem.Common;
using HabitSystem.Domain.Enums;
using HabitSystem.Features.Auth;
using HabitSystem.Infrastructure;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace HabitSystem.Features.Account;

public static class GetPlan
{
    public record Query : IRequest<Result<Response>>;

    public record Response(
        string Plan,
        bool IsEmailVerified,
        int ActiveHabits,
        int HabitLimit   // -1 = unlimited
    );

    public class Handler : IRequestHandler<Query, Result<Response>>
    {
        private readonly AppDbContext _db;
        private readonly IHttpContextAccessor _http;

        public Handler(AppDbContext db, IHttpContextAccessor http)
        {
            _db = db;
            _http = http;
        }

        public async Task<Result<Response>> Handle(Query request, CancellationToken ct)
        {
            var userId = _http.HttpContext?.User.GetUserId();
            if (userId == null) return Result<Response>.Failure("Not authenticated");

            var user = await _db.Users.FindAsync(new object[] { userId.Value }, ct);
            if (user == null) return Result<Response>.Failure("User not found");

            var activeHabits = await _db.Habits
                .CountAsync(h => h.UserId == userId.Value && h.IsActive, ct);

            var habitLimit = user.Plan == Plan.Free ? Constants.FreePlanHabitLimit : -1;

            return Result<Response>.Success(new Response(
                user.Plan.ToString(),
                user.IsEmailVerified,
                activeHabits,
                habitLimit
            ));
        }
    }

    public static void MapGetPlanEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/account/plan", [Authorize] async (IMediator mediator) =>
        {
            var result = await mediator.Send(new Query());
            return result.IsSuccess
                ? Results.Ok(result.Value)
                : Results.BadRequest(new { error = result.Error });
        })
        .WithName("GetPlan")
        .WithOpenApi()
        .RequireAuthorization();
    }
}
