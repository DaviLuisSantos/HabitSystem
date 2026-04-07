using HabitSystem.Common;
using HabitSystem.Domain.Enums;
using HabitSystem.Infrastructure;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HabitSystem.Features.Habits;

// Request
public record UpdateHabitRequest(
    string Name,
    string? Description,
    short Weight,
    short PartialWeight,
    FrequencyType FrequencyType,
    int[]? FrequencyDays,
    short? FrequencyTimes
);

// Handler
public class UpdateHabitHandler : IRequestHandler<UpdateHabitCommand, Result<HabitDto>>
{
    private readonly AppDbContext _db;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public UpdateHabitHandler(AppDbContext db, IHttpContextAccessor httpContextAccessor)
    {
        _db = db;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task<Result<HabitDto>> Handle(UpdateHabitCommand request, CancellationToken cancellationToken)
    {
        // Get authenticated user ID
        var userId = _httpContextAccessor.HttpContext?.User.GetUserId();
        if (userId == null)
            return Result<HabitDto>.Failure("User not authenticated");

        // Validation
        if (string.IsNullOrWhiteSpace(request.Request.Name))
            return Result<HabitDto>.Failure("Habit name is required");

        if (request.Request.Weight < 1 || request.Request.Weight > 10)
            return Result<HabitDto>.Failure("Weight must be between 1 and 10");

        if (request.Request.PartialWeight < 0 || request.Request.PartialWeight > 10)
            return Result<HabitDto>.Failure("Partial weight must be between 0 and 10");

        if (request.Request.FrequencyType == FrequencyType.SpecificDays && 
            (request.Request.FrequencyDays == null || request.Request.FrequencyDays.Length == 0))
            return Result<HabitDto>.Failure("FrequencyDays is required for SpecificDays frequency type");

        if (request.Request.FrequencyType == FrequencyType.XTimesWeek && 
            (request.Request.FrequencyTimes == null || request.Request.FrequencyTimes < 1))
            return Result<HabitDto>.Failure("FrequencyTimes is required for XTimesWeek frequency type");

        // Find habit
        var habit = await _db.Habits
            .FirstOrDefaultAsync(h => h.Id == request.Id && h.UserId == userId.Value, cancellationToken);

        if (habit == null)
            return Result<HabitDto>.Failure("Habit not found");

        // Update habit
        habit.Name = request.Request.Name;
        habit.Description = request.Request.Description;
        habit.Weight = request.Request.Weight;
        habit.PartialWeight = request.Request.PartialWeight;
        habit.FrequencyType = request.Request.FrequencyType;
        habit.FrequencyDays = request.Request.FrequencyDays;
        habit.FrequencyTimes = request.Request.FrequencyTimes;

        await _db.SaveChangesAsync(cancellationToken);

        var response = new HabitDto(
            habit.Id,
            habit.Name,
            habit.Description,
            habit.Weight,
            habit.PartialWeight,
            habit.FrequencyType,
            habit.FrequencyDays,
            habit.FrequencyTimes,
            habit.IsActive,
            habit.CreatedAt,
            habit.ArchivedAt
        );

        return Result<HabitDto>.Success(response);
    }
}

// Command
public record UpdateHabitCommand(Guid Id, UpdateHabitRequest Request) : IRequest<Result<HabitDto>>;

// Endpoint
public static class UpdateHabitEndpoint
{
    public static IEndpointRouteBuilder MapUpdateHabit(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPut("/api/habits/{id:guid}", [Authorize] async (
            [FromRoute] Guid id,
            [FromBody] UpdateHabitRequest request,
            [FromServices] IMediator mediator) =>
        {
            var result = await mediator.Send(new UpdateHabitCommand(id, request));
            
            return result.IsSuccess 
                ? Results.Ok(result.Value)
                : Results.BadRequest(new { error = result.Error });
        })
        .WithName("UpdateHabit")
        .WithOpenApi()
        .RequireAuthorization();

        return endpoints;
    }
}
