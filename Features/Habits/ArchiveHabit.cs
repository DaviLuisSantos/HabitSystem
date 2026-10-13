using HabitSystem.Common;
using HabitSystem.Infrastructure;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HabitSystem.Features.Habits;

// Handler
public class ArchiveHabitHandler : IRequestHandler<ArchiveHabitCommand, Result>
{
    private readonly AppDbContext _db;
    private readonly IMediator _mediator;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public ArchiveHabitHandler(AppDbContext db, IMediator mediator, IHttpContextAccessor httpContextAccessor)
    {
        _db = db;
        _mediator = mediator;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task<Result> Handle(ArchiveHabitCommand request, CancellationToken cancellationToken)
    {
        // Get authenticated user ID
        var userId = _httpContextAccessor.HttpContext?.User.GetUserId();
        if (userId == null)
            return Result.Failure("User not authenticated", ErrorCodes.Unauthorized);

        var habit = await _db.Habits
            .FirstOrDefaultAsync(h => h.Id == request.Id && h.UserId == userId.Value, cancellationToken);

        if (habit == null)
            return Result.Failure("Habit not found", ErrorCodes.NotFound);

        // Soft delete - preserve history. Idempotent: keep the original ArchivedAt.
        if (habit.IsActive || habit.ArchivedAt == null)
        {
            habit.IsActive = false;
            habit.ArchivedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(cancellationToken);
        }

        // Habit no longer counts from today on
        var today = await _db.GetUserTodayAsync(userId.Value, cancellationToken);
        await _mediator.Send(new Scores.RecalculateScoreCommand(today, userId.Value), cancellationToken);

        return Result.Success();
    }
}

// Command
public record ArchiveHabitCommand(Guid Id) : IRequest<Result>;

// Endpoint
public static class ArchiveHabitEndpoint
{
    public static IEndpointRouteBuilder MapArchiveHabit(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapDelete("/api/habits/{id:guid}", [Authorize] async (
            [FromRoute] Guid id,
            [FromServices] IMediator mediator) =>
        {
            var result = await mediator.Send(new ArchiveHabitCommand(id));

            return result.IsSuccess
                ? Results.NoContent()
                : result.ToErrorResult();
        })
        .WithName("ArchiveHabit")
        .WithOpenApi()
        .RequireAuthorization();

        return endpoints;
    }
}
