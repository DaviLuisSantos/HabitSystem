using HabitSystem.Common;
using HabitSystem.Infrastructure;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HabitSystem.Features.Habits;

// Handler
public class ArchiveHabitHandler : IRequestHandler<ArchiveHabitCommand, Result>
{
    private readonly AppDbContext _db;

    public ArchiveHabitHandler(AppDbContext db)
    {
        _db = db;
    }

    public async Task<Result> Handle(ArchiveHabitCommand request, CancellationToken cancellationToken)
    {
        var habit = await _db.Habits
            .FirstOrDefaultAsync(h => h.Id == request.Id && h.UserId == Constants.DefaultUserId, cancellationToken);

        if (habit == null)
            return Result.Failure("Habit not found");

        // Soft delete - preserve history
        habit.IsActive = false;
        habit.ArchivedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);

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
        endpoints.MapDelete("/api/habits/{id:guid}", async (
            [FromRoute] Guid id,
            [FromServices] IMediator mediator) =>
        {
            var result = await mediator.Send(new ArchiveHabitCommand(id));
            
            return result.IsSuccess 
                ? Results.NoContent()
                : Results.NotFound(new { error = result.Error });
        })
        .WithName("ArchiveHabit")
        .WithOpenApi();

        return endpoints;
    }
}
