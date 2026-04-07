using HabitSystem.Common;
using HabitSystem.Infrastructure;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HabitSystem.Features.Scores;

// Response
public record DailyScoreDto(
    DateOnly Date,
    short TotalPossible,
    short TotalEarned,
    decimal Percentage,
    DateTime CalculatedAt
);

// Handler
public class GetDailyScoreHandler : IRequestHandler<GetDailyScoreQuery, Result<DailyScoreDto>>
{
    private readonly AppDbContext _db;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public GetDailyScoreHandler(AppDbContext db, IHttpContextAccessor httpContextAccessor)
    {
        _db = db;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task<Result<DailyScoreDto>> Handle(GetDailyScoreQuery request, CancellationToken cancellationToken)
    {
        // Get authenticated user ID
        var userId = _httpContextAccessor.HttpContext?.User.GetUserId();
        if (userId == null)
            return Result<DailyScoreDto>.Failure("User not authenticated");

        var score = await _db.DailyScores
            .Where(d => d.UserId == userId.Value && d.Date == request.Date)
            .Select(d => new DailyScoreDto(
                d.Date,
                d.TotalPossible,
                d.TotalEarned,
                d.Percentage,
                d.CalculatedAt
            ))
            .FirstOrDefaultAsync(cancellationToken);

        // If no score exists, calculate it
        if (score == null)
        {
            var calculator = new ScoreCalculator(_db);
            var calculatedScore = await calculator.CalculateScore(request.Date, userId.Value, cancellationToken);
            
            score = new DailyScoreDto(
                calculatedScore.Date,
                calculatedScore.TotalPossible,
                calculatedScore.TotalEarned,
                calculatedScore.Percentage,
                calculatedScore.CalculatedAt
            );
        }

        return Result<DailyScoreDto>.Success(score);
    }
}

// Query
public record GetDailyScoreQuery(DateOnly Date) : IRequest<Result<DailyScoreDto>>;

// Endpoint
public static class GetDailyScoreEndpoint
{
    public static IEndpointRouteBuilder MapGetDailyScore(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/scores/today", [Authorize] async ([FromServices] IMediator mediator) =>
        {
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var result = await mediator.Send(new GetDailyScoreQuery(today));
            
            return result.IsSuccess 
                ? Results.Ok(result.Value)
                : Results.BadRequest(new { error = result.Error });
        })
        .WithName("GetTodayScore")
        .WithOpenApi()
        .RequireAuthorization();

        endpoints.MapGet("/api/scores/{date}", [Authorize] async (
            [FromRoute] DateOnly date,
            [FromServices] IMediator mediator) =>
        {
            var result = await mediator.Send(new GetDailyScoreQuery(date));
            
            return result.IsSuccess 
                ? Results.Ok(result.Value)
                : Results.BadRequest(new { error = result.Error });
        })
        .WithName("GetDailyScore")
        .WithOpenApi()
        .RequireAuthorization();

        return endpoints;
    }
}
