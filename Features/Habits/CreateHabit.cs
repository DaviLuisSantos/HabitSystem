using HabitSystem.Common;
using HabitSystem.Domain;
using HabitSystem.Domain.Enums;
using HabitSystem.Infrastructure;
using MediatR;
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

    public CreateHabitHandler(AppDbContext db, IMediator mediator)
    {
        _db = db;
        _mediator = mediator;
    }

    public async Task<Result<CreateHabitResponse>> Handle(CreateHabitCommand request, CancellationToken cancellationToken)
    {
        // Validation
        if (string.IsNullOrWhiteSpace(request.Request.Name))
            return Result<CreateHabitResponse>.Failure("Habit name is required");

        if (request.Request.Weight < 1 || request.Request.Weight > 10)
            return Result<CreateHabitResponse>.Failure("Weight must be between 1 and 10");

        if (request.Request.PartialWeight < 0 || request.Request.PartialWeight > 10)
            return Result<CreateHabitResponse>.Failure("Partial weight must be between 0 and 10");

        if (request.Request.FrequencyType == FrequencyType.SpecificDays && 
            (request.Request.FrequencyDays == null || request.Request.FrequencyDays.Length == 0))
            return Result<CreateHabitResponse>.Failure("FrequencyDays is required for SpecificDays frequency type");

        if (request.Request.FrequencyType == FrequencyType.XTimesWeek && 
            (request.Request.FrequencyTimes == null || request.Request.FrequencyTimes < 1))
            return Result<CreateHabitResponse>.Failure("FrequencyTimes is required for XTimesWeek frequency type");

        // Create habit
        var habit = new Habit
        {
            Id = Guid.NewGuid(),
            UserId = Constants.DefaultUserId,
            Name = request.Request.Name,
            Description = request.Request.Description,
            Weight = request.Request.Weight,
            PartialWeight = request.Request.PartialWeight,
            FrequencyType = request.Request.FrequencyType,
            FrequencyDays = request.Request.FrequencyDays,
            FrequencyTimes = request.Request.FrequencyTimes,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        _db.Habits.Add(habit);
        await _db.SaveChangesAsync(cancellationToken);

        // Recalculate score for today since a new habit was added
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        await _mediator.Send(new Scores.RecalculateScoreCommand(today), cancellationToken);

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
        endpoints.MapPost("/api/habits", async (
            [FromBody] CreateHabitRequest request,
            [FromServices] IMediator mediator) =>
        {
            var result = await mediator.Send(new CreateHabitCommand(request));
            
            return result.IsSuccess 
                ? Results.Created($"/api/habits/{result.Value!.Id}", result.Value)
                : Results.BadRequest(new { error = result.Error });
        })
        .WithName("CreateHabit")
        .WithOpenApi();

        return endpoints;
    }
}
