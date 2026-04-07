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
            return Result<List<DailyScoreDto>>.Failure("User not authenticated");

        // Get Monday of the week containing the given date
        var startOfWeek = request.Date.AddDays(-(int)request.Date.DayOfWeek + (request.Date.DayOfWeek == DayOfWeek.Sunday ? -6 : 1));
        var endOfWeek = startOfWeek.AddDays(6);

        // Always recalculate scores for accurate data
        var calculator = new ScoreCalculator(_db);
        var scores = new List<DailyScoreDto>();
        
        // Use client's "today" to determine which days to calculate
        var clientToday = request.ClientToday ?? DateOnly.FromDateTime(DateTime.UtcNow);
        
        for (var date = startOfWeek; date <= endOfWeek; date = date.AddDays(1))
        {
            // Only calculate for days up to client's today
            if (date <= clientToday)
            {
                var calculatedScore = await calculator.CalculateScore(date, userId.Value, cancellationToken);
                scores.Add(new DailyScoreDto(
                    calculatedScore.Date,
                    calculatedScore.TotalPossible,
                    calculatedScore.TotalEarned,
                    calculatedScore.Percentage,
                    calculatedScore.CalculatedAt
                ));
            }
        }

        return Result<List<DailyScoreDto>>.Success(scores.OrderBy(s => s.Date).ToList());
    }
}

// Query
public record GetWeeklyScoresQuery(DateOnly Date, DateOnly? ClientToday = null) : IRequest<Result<List<DailyScoreDto>>>;

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
            var targetDate = date ?? DateOnly.FromDateTime(DateTime.UtcNow);
            var result = await mediator.Send(new GetWeeklyScoresQuery(targetDate, today));
            
            return result.IsSuccess 
                ? Results.Ok(result.Value)
                : Results.BadRequest(new { error = result.Error });
        })
        .WithName("GetWeeklyScores")
        .WithOpenApi()
        .RequireAuthorization();

        return endpoints;
    }
}
