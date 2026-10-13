using HabitSystem.Common;
using HabitSystem.Infrastructure;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HabitSystem.Features.Habits;

// Handler
public class RestoreHabitHandler : IRequestHandler<RestoreHabitCommand, Result<HabitDto>>
{
    private readonly AppDbContext _db;
    private readonly IMediator _mediator;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public RestoreHabitHandler(AppDbContext db, IMediator mediator, IHttpContextAccessor httpContextAccessor)
    {
        _db = db;
        _mediator = mediator;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task<Result<HabitDto>> Handle(RestoreHabitCommand request, CancellationToken cancellationToken)
    {
        // Get authenticated user ID
        var userId = _httpContextAccessor.HttpContext?.User.GetUserId();
        if (userId == null)
            return Result<HabitDto>.Failure("User not authenticated", ErrorCodes.Unauthorized);

        var habit = await _db.Habits
            .FirstOrDefaultAsync(h => h.Id == request.Id && h.UserId == userId.Value, cancellationToken);

        if (habit == null)
            return Result<HabitDto>.Failure("Habit not found", ErrorCodes.NotFound);

        // Idempotent: restoring an active habit is a no-op
        if (!habit.IsActive || habit.ArchivedAt != null)
        {
            habit.IsActive = true;
            habit.ArchivedAt = null;
            await _db.SaveChangesAsync(cancellationToken);
        }

        // Habit counts again for today
        var today = await _db.GetUserTodayAsync(userId.Value, cancellationToken);
        await _mediator.Send(new Scores.RecalculateScoreCommand(today, userId.Value), cancellationToken);

        return Result<HabitDto>.Success(habit.ToDto());
    }
}

// Command
public record RestoreHabitCommand(Guid Id) : IRequest<Result<HabitDto>>;

// Endpoint
public static class RestoreHabitEndpoint
{
    public static IEndpointRouteBuilder MapRestoreHabit(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/habits/{id:guid}/restore", [Authorize] async (
            [FromRoute] Guid id,
            [FromServices] IMediator mediator) =>
        {
            var result = await mediator.Send(new RestoreHabitCommand(id));

            return result.IsSuccess
                ? Results.Ok(result.Value)
                : result.ToErrorResult();
        })
        .WithName("RestoreHabit")
        .WithOpenApi()
        .RequireAuthorization();

        return endpoints;
    }
}
