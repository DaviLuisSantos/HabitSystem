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
    private readonly IHttpContextAccessor _httpContextAccessor;

    public ArchiveHabitHandler(AppDbContext db, IHttpContextAccessor httpContextAccessor)
    {
        _db = db;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task<Result> Handle(ArchiveHabitCommand request, CancellationToken cancellationToken)
    {
        // Get authenticated user ID
        var userId = _httpContextAccessor.HttpContext?.User.GetUserId();
        if (userId == null)
            return Result.Failure("User not authenticated");

        var habit = await _db.Habits
            .FirstOrDefaultAsync(h => h.Id == request.Id && h.UserId == userId.Value, cancellationToken);

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
        endpoints.MapDelete("/api/habits/{id:guid}", [Authorize] async (
            [FromRoute] Guid id,
            [FromServices] IMediator mediator) =>
        {
            var result = await mediator.Send(new ArchiveHabitCommand(id));
            
            return result.IsSuccess 
                ? Results.NoContent()
                : Results.NotFound(new { error = result.Error });
        })
        .WithName("ArchiveHabit")
        .WithOpenApi()
        .RequireAuthorization();

        return endpoints;
    }
}
