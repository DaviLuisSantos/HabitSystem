using HabitSystem.Common;
using HabitSystem.Domain.Enums;
using HabitSystem.Infrastructure;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HabitSystem.Features.Habits;

// Response
public record HabitDto(
    Guid Id,
    string Name,
    string? Description,
    short Weight,
    short PartialWeight,
    FrequencyType FrequencyType,
    int[]? FrequencyDays,
    short? FrequencyTimes,
    bool IsActive,
    DateTime CreatedAt,
    DateTime? ArchivedAt
);

// Handler
public class GetHabitsHandler : IRequestHandler<GetHabitsQuery, Result<List<HabitDto>>>
{
    private readonly AppDbContext _db;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public GetHabitsHandler(AppDbContext db, IHttpContextAccessor httpContextAccessor)
    {
        _db = db;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task<Result<List<HabitDto>>> Handle(GetHabitsQuery request, CancellationToken cancellationToken)
    {
        // Get authenticated user ID
        var userId = _httpContextAccessor.HttpContext?.User.GetUserId();
        if (userId == null)
            return Result<List<HabitDto>>.Failure("User not authenticated");

        var query = _db.Habits
            .Where(h => h.UserId == userId.Value);

        // Filter by active status if specified
        if (request.ActiveOnly)
            query = query.Where(h => h.IsActive);

        var habits = await query
            .OrderBy(h => h.Name)
            .Select(h => new HabitDto(
                h.Id,
                h.Name,
                h.Description,
                h.Weight,
                h.PartialWeight,
                h.FrequencyType,
                h.FrequencyDays,
                h.FrequencyTimes,
                h.IsActive,
                h.CreatedAt,
                h.ArchivedAt
            ))
            .ToListAsync(cancellationToken);

        return Result<List<HabitDto>>.Success(habits);
    }
}

// Query
public record GetHabitsQuery(bool ActiveOnly = true) : IRequest<Result<List<HabitDto>>>;

// Endpoint
public static class GetHabitsEndpoint
{
    public static IEndpointRouteBuilder MapGetHabits(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/habits", [Authorize] async (
            [FromQuery] bool? activeOnly,
            [FromServices] IMediator mediator) =>
        {
            var result = await mediator.Send(new GetHabitsQuery(activeOnly ?? true));
            
            return result.IsSuccess 
                ? Results.Ok(result.Value)
                : Results.BadRequest(new { error = result.Error });
        })
        .WithName("GetHabits")
        .WithOpenApi()
        .RequireAuthorization();

        return endpoints;
    }
}
