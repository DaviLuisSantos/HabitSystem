using HabitSystem.Common;
using HabitSystem.Infrastructure;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HabitSystem.Features.Scores;

// Handler
public class GetWeeklyScoresHandler : IRequestHandler<GetWeeklyScoresQuery, Result<List<DailyScoreDto>>>
{
    private readonly AppDbContext _db;

    public GetWeeklyScoresHandler(AppDbContext db)
    {
        _db = db;
    }

    public async Task<Result<List<DailyScoreDto>>> Handle(GetWeeklyScoresQuery request, CancellationToken cancellationToken)
    {
        // Get Monday of the week containing the given date
        var startOfWeek = request.Date.AddDays(-(int)request.Date.DayOfWeek + (request.Date.DayOfWeek == DayOfWeek.Sunday ? -6 : 1));
        var endOfWeek = startOfWeek.AddDays(6);

        var scores = await _db.DailyScores
            .Where(d => d.UserId == Constants.DefaultUserId && 
                        d.Date >= startOfWeek && 
                        d.Date <= endOfWeek)
            .OrderBy(d => d.Date)
            .Select(d => new DailyScoreDto(
                d.Date,
                d.TotalPossible,
                d.TotalEarned,
                d.Percentage,
                d.CalculatedAt
            ))
            .ToListAsync(cancellationToken);

        // Calculate missing days if needed
        var calculator = new ScoreCalculator(_db);
        var existingDates = scores.Select(s => s.Date).ToHashSet();
        
        for (var date = startOfWeek; date <= endOfWeek; date = date.AddDays(1))
        {
            if (!existingDates.Contains(date) && date <= DateOnly.FromDateTime(DateTime.UtcNow))
            {
                var calculatedScore = await calculator.CalculateScore(date, cancellationToken);
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
public record GetWeeklyScoresQuery(DateOnly Date) : IRequest<Result<List<DailyScoreDto>>>;

// Endpoint
public static class GetWeeklyScoresEndpoint
{
    public static IEndpointRouteBuilder MapGetWeeklyScores(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/scores/week", async (
            [FromQuery] DateOnly? date,
            [FromServices] IMediator mediator) =>
        {
            var targetDate = date ?? DateOnly.FromDateTime(DateTime.UtcNow);
            var result = await mediator.Send(new GetWeeklyScoresQuery(targetDate));
            
            return result.IsSuccess 
                ? Results.Ok(result.Value)
                : Results.BadRequest(new { error = result.Error });
        })
        .WithName("GetWeeklyScores")
        .WithOpenApi();

        return endpoints;
    }
}
