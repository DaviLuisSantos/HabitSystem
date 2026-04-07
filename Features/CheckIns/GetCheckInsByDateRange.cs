using HabitSystem.Common;
using HabitSystem.Infrastructure;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HabitSystem.Features.CheckIns;

// Handler
public class GetCheckInsByDateRangeHandler : IRequestHandler<GetCheckInsByDateRangeQuery, Result<List<CheckInDto>>>
{
    private readonly AppDbContext _db;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public GetCheckInsByDateRangeHandler(AppDbContext db, IHttpContextAccessor httpContextAccessor)
    {
        _db = db;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task<Result<List<CheckInDto>>> Handle(GetCheckInsByDateRangeQuery request, CancellationToken cancellationToken)
    {
        // Get authenticated user ID
        var userId = _httpContextAccessor.HttpContext?.User.GetUserId();
        if (userId == null)
            return Result<List<CheckInDto>>.Failure("User not authenticated");

        var checkIns = await _db.CheckIns
            .Include(c => c.Habit)
            .Where(c => c.UserId == userId.Value && 
                        c.Date >= request.StartDate && 
                        c.Date <= request.EndDate)
            .OrderBy(c => c.Date)
            .ThenBy(c => c.Habit.Name)
            .Select(c => new CheckInDto(
                c.Id,
                c.HabitId,
                c.Habit.Name,
                c.Date,
                c.Status,
                c.Note,
                c.CreatedAt
            ))
            .ToListAsync(cancellationToken);

        return Result<List<CheckInDto>>.Success(checkIns);
    }
}

// Query
public record GetCheckInsByDateRangeQuery(DateOnly StartDate, DateOnly EndDate) : IRequest<Result<List<CheckInDto>>>;

// Endpoint
public static class GetCheckInsByDateRangeEndpoint
{
    public static IEndpointRouteBuilder MapGetCheckInsByDateRange(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/checkins", [Authorize] async (
            [FromQuery] DateOnly startDate,
            [FromQuery] DateOnly endDate,
            [FromServices] IMediator mediator) =>
        {
            var result = await mediator.Send(new GetCheckInsByDateRangeQuery(startDate, endDate));
            
            return result.IsSuccess 
                ? Results.Ok(result.Value)
                : Results.BadRequest(new { error = result.Error });
        })
        .WithName("GetCheckInsByDateRange")
        .WithOpenApi()
        .RequireAuthorization();

        return endpoints;
    }
}
