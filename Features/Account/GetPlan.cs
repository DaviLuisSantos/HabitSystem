using HabitSystem.Common;
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
        int? HabitLimit   // null = unlimited (no plan limits in v1.0)
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
            if (userId == null) return Result<Response>.Failure("Not authenticated", ErrorCodes.Unauthorized);

            var user = await _db.Users.FindAsync(new object[] { userId.Value }, ct);
            if (user == null) return Result<Response>.Failure("User not found", ErrorCodes.Unauthorized);

            var activeHabits = await _db.Habits
                .CountAsync(h => h.UserId == userId.Value && h.IsActive, ct);

            return Result<Response>.Success(new Response(
                user.Plan.ToString(),
                user.IsEmailVerified,
                activeHabits,
                null // v1.0: habit limit removed for every plan
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
                : result.ToErrorResult();
        })
        .WithName("GetPlan")
        .WithOpenApi()
        .RequireAuthorization();
    }
}
