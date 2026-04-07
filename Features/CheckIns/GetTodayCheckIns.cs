using HabitSystem.Common;
using HabitSystem.Infrastructure;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HabitSystem.Features.CheckIns;

// Handler
public class GetTodayCheckInsHandler : IRequestHandler<GetTodayCheckInsQuery, Result<List<CheckInDto>>>
{
    private readonly AppDbContext _db;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public GetTodayCheckInsHandler(AppDbContext db, IHttpContextAccessor httpContextAccessor)
    {
        _db = db;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task<Result<List<CheckInDto>>> Handle(GetTodayCheckInsQuery request, CancellationToken cancellationToken)
    {
        // Get authenticated user ID
        var userId = _httpContextAccessor.HttpContext?.User.GetUserId();
        if (userId == null)
            return Result<List<CheckInDto>>.Failure("User not authenticated");

        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var checkIns = await _db.CheckIns
            .Include(c => c.Habit)
            .Where(c => c.UserId == userId.Value && c.Date == today)
            .OrderBy(c => c.Habit.Name)
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
public record GetTodayCheckInsQuery : IRequest<Result<List<CheckInDto>>>;

// Endpoint
public static class GetTodayCheckInsEndpoint
{
    public static IEndpointRouteBuilder MapGetTodayCheckIns(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/checkins/today", [Authorize] async ([FromServices] IMediator mediator) =>
        {
            var result = await mediator.Send(new GetTodayCheckInsQuery());
            
            return result.IsSuccess 
                ? Results.Ok(result.Value)
                : Results.BadRequest(new { error = result.Error });
        })
        .WithName("GetTodayCheckIns")
        .WithOpenApi()
        .RequireAuthorization();

        return endpoints;
    }
}
