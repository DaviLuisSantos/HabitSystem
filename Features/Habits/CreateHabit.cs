using HabitSystem.Common;
using HabitSystem.Domain;
using HabitSystem.Domain.Enums;
using HabitSystem.Infrastructure;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HabitSystem.Features.Habits;

// Request & Response
public record CreateHabitRequest(
    string Name,
    string? Description,
    short Weight,
    short PartialWeight,
    FrequencyType FrequencyType,
    int[]? FrequencyDays,
    short? FrequencyTimes
);

public record CreateHabitResponse(
    Guid Id,
    string Name,
    string? Description,
    short Weight,
    short PartialWeight,
    FrequencyType FrequencyType,
    int[]? FrequencyDays,
    short? FrequencyTimes,
    bool IsActive,
    DateTime CreatedAt
);

// Handler
public class CreateHabitHandler : IRequestHandler<CreateHabitCommand, Result<CreateHabitResponse>>
{
    private readonly AppDbContext _db;
    private readonly IMediator _mediator;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CreateHabitHandler(AppDbContext db, IMediator mediator, IHttpContextAccessor httpContextAccessor)
    {
        _db = db;
        _mediator = mediator;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task<Result<CreateHabitResponse>> Handle(CreateHabitCommand request, CancellationToken cancellationToken)
    {
        // Get authenticated user ID
        var userId = _httpContextAccessor.HttpContext?.User.GetUserId();
        if (userId == null)
            return Result<CreateHabitResponse>.Failure("User not authenticated", ErrorCodes.Unauthorized);

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
            return Result<CreateHabitResponse>.Failure(validation.Error!, validation.Code!);

        var input = validation.Value!;

        // v1.0: no plan-based habit limit

        // Create habit
        var habit = new Habit
        {
            Id = Guid.NewGuid(),
            UserId = userId.Value,
            Name = input.Name,
            Description = input.Description,
            Weight = input.Weight,
            PartialWeight = input.PartialWeight,
            FrequencyType = input.FrequencyType,
            FrequencyDays = input.FrequencyDays,
            FrequencyTimes = input.FrequencyTimes,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        _db.Habits.Add(habit);
        await _db.SaveChangesAsync(cancellationToken);

        // Recalculate score for the user's today since a new habit was added
        var today = await _db.GetUserTodayAsync(userId.Value, cancellationToken);
        await _mediator.Send(new Scores.RecalculateScoreCommand(today, userId.Value), cancellationToken);

        var response = new CreateHabitResponse(
            habit.Id,
            habit.Name,
            habit.Description,
            habit.Weight,
            habit.PartialWeight,
            habit.FrequencyType,
            habit.FrequencyDays,
            habit.FrequencyTimes,
            habit.IsActive,
            habit.CreatedAt
        );

        return Result<CreateHabitResponse>.Success(response);
    }
}

// Command
public record CreateHabitCommand(CreateHabitRequest Request) : IRequest<Result<CreateHabitResponse>>;

// Endpoint
public static class CreateHabitEndpoint
{
    public static IEndpointRouteBuilder MapCreateHabit(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/habits", [Authorize] async (
            [FromBody] CreateHabitRequest request,
            [FromServices] IMediator mediator) =>
        {
            var result = await mediator.Send(new CreateHabitCommand(request));
            
            return result.IsSuccess 
                ? Results.Created($"/api/habits/{result.Value!.Id}", result.Value)
                : result.ToErrorResult();
        })
        .WithName("CreateHabit")
        .WithOpenApi()
        .RequireAuthorization();

        return endpoints;
    }
}
