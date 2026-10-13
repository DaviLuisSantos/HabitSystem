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
    private readonly IMediator _mediator;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public UpdateHabitHandler(AppDbContext db, IMediator mediator, IHttpContextAccessor httpContextAccessor)
    {
        _db = db;
        _mediator = mediator;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task<Result<HabitDto>> Handle(UpdateHabitCommand request, CancellationToken cancellationToken)
    {
        // Get authenticated user ID
        var userId = _httpContextAccessor.HttpContext?.User.GetUserId();
        if (userId == null)
            return Result<HabitDto>.Failure("User not authenticated", ErrorCodes.Unauthorized);

        // Validation (also normalizes FrequencyDays to 0=Sunday..6=Saturday)
        var validation = HabitValidation.Validate(
            request.Request.Name,
            request.Request.Description,
            request.Request.Weight,
            request.Request.PartialWeight,
            request.Request.FrequencyType,
            request.Request.FrequencyDays,
            request.Request.FrequencyTimes);

        if (!validation.IsSuccess)
            return Result<HabitDto>.Failure(validation.Error!, validation.Code!);

        var input = validation.Value!;

        // Find habit
        var habit = await _db.Habits
            .FirstOrDefaultAsync(h => h.Id == request.Id && h.UserId == userId.Value, cancellationToken);

        if (habit == null)
            return Result<HabitDto>.Failure("Habit not found", ErrorCodes.NotFound);

        // Update habit
        habit.Name = input.Name;
        habit.Description = input.Description;
        habit.Weight = input.Weight;
        habit.PartialWeight = input.PartialWeight;
        habit.FrequencyType = input.FrequencyType;
        habit.FrequencyDays = input.FrequencyDays;
        habit.FrequencyTimes = input.FrequencyTimes;

        await _db.SaveChangesAsync(cancellationToken);

        // Weights / frequency changed: refresh today's cached score
        var today = await _db.GetUserTodayAsync(userId.Value, cancellationToken);
        await _mediator.Send(new Scores.RecalculateScoreCommand(today, userId.Value), cancellationToken);

        var response = habit.ToDto();

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
                : result.ToErrorResult();
        })
        .WithName("UpdateHabit")
        .WithOpenApi()
        .RequireAuthorization();

        return endpoints;
    }
}
