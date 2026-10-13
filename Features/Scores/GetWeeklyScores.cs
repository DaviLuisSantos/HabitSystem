using HabitSystem.Common;
using HabitSystem.Infrastructure;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HabitSystem.Features.Scores;

// Handler
public class GetWeeklyScoresHandler : IRequestHandler<GetWeeklyScoresQuery, Result<List<DailyScoreDto>>>
{
    private readonly AppDbContext _db;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public GetWeeklyScoresHandler(AppDbContext db, IHttpContextAccessor httpContextAccessor)
    {
        _db = db;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task<Result<List<DailyScoreDto>>> Handle(GetWeeklyScoresQuery request, CancellationToken cancellationToken)
    {
        // Get authenticated user ID
        var userId = _httpContextAccessor.HttpContext?.User.GetUserId();
        if (userId == null)
            return Result<List<DailyScoreDto>>.Failure("User not authenticated", ErrorCodes.Unauthorized);

        // User's "today" (User.Timezone) is the fallback for both dates
        var userToday = await _db.GetUserTodayAsync(userId.Value, cancellationToken);
        var referenceDate = request.Date ?? userToday;

        // Get Monday of the week containing the given date
        var startOfWeek = referenceDate.AddDays(-(int)referenceDate.DayOfWeek + (referenceDate.DayOfWeek == DayOfWeek.Sunday ? -6 : 1));
        var endOfWeek = startOfWeek.AddDays(6);

        // Only calculate for days up to client's today
        var clientToday = request.ClientToday ?? userToday;
        var lastDate = endOfWeek < clientToday ? endOfWeek : clientToday;

        // Computed on read (no DB writes)
        var scores = new List<DailyScoreDto>();
        if (lastDate >= startOfWeek)
        {
            var calculator = new ScoreCalculator(_db);
            var calculated = await calculator.ComputeScores(startOfWeek, lastDate, userId.Value, cancellationToken);
            scores = calculated
                .Select(s => new DailyScoreDto(s.Date, s.TotalPossible, s.TotalEarned, s.Percentage, s.CalculatedAt))
                .ToList();
        }

        return Result<List<DailyScoreDto>>.Success(scores.OrderBy(s => s.Date).ToList());
    }
}

// Query
public record GetWeeklyScoresQuery(DateOnly? Date, DateOnly? ClientToday = null) : IRequest<Result<List<DailyScoreDto>>>;

// Endpoint
public static class GetWeeklyScoresEndpoint
{
    public static IEndpointRouteBuilder MapGetWeeklyScores(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/scores/week", [Authorize] async (
            [FromQuery] DateOnly? date,
            [FromQuery] DateOnly? today,
            [FromServices] IMediator mediator) =>
        {
            var result = await mediator.Send(new GetWeeklyScoresQuery(date, today));
            
            return result.IsSuccess 
                ? Results.Ok(result.Value)
                : result.ToErrorResult();
        })
        .WithName("GetWeeklyScores")
        .WithOpenApi()
        .RequireAuthorization();

        return endpoints;
    }
}
