using HabitSystem.Common;
using HabitSystem.Domain;
using HabitSystem.Domain.Enums;
using HabitSystem.Infrastructure;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace HabitSystem.Features.CheckIns;

// Request & Response
public record CreateCheckInRequest(
    Guid HabitId,
    DateOnly Date,
    CheckInStatus Status,
    string? Note
);

public record CheckInDto(
    Guid Id,
    Guid HabitId,
    string HabitName,
    DateOnly Date,
    CheckInStatus Status,
    string? Note,
    DateTime CreatedAt
);

// Handler
public class CreateCheckInHandler : IRequestHandler<CreateCheckInCommand, Result<CheckInDto>>
{
    private readonly AppDbContext _db;
    private readonly IMediator _mediator;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CreateCheckInHandler(AppDbContext db, IMediator mediator, IHttpContextAccessor httpContextAccessor)
    {
        _db = db;
        _mediator = mediator;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task<Result<CheckInDto>> Handle(CreateCheckInCommand request, CancellationToken cancellationToken)
    {
        // Get authenticated user ID
        var userId = _httpContextAccessor.HttpContext?.User.GetUserId();
        if (userId == null)
            return Result<CheckInDto>.Failure("User not authenticated", ErrorCodes.Unauthorized);

        // Input validation
        if (!Enum.IsDefined(request.Request.Status))
            return Result<CheckInDto>.Failure("Invalid check-in status");

        var note = string.IsNullOrWhiteSpace(request.Request.Note) ? null : request.Request.Note.Trim();
        if (note != null && note.Length > Constants.MaxNoteLength)
            return Result<CheckInDto>.Failure($"Note must be at most {Constants.MaxNoteLength} characters");

        // Date must be within [today - 365, today] in the user's timezone
        var today = await _db.GetUserTodayAsync(userId.Value, cancellationToken);
        if (request.Request.Date > today)
            return Result<CheckInDto>.Failure("Check-in date cannot be in the future");

        if (request.Request.Date < today.AddDays(-Constants.MaxCheckInAgeDays))
            return Result<CheckInDto>.Failure($"Check-in date cannot be older than {Constants.MaxCheckInAgeDays} days");

        // Validate habit exists and is active
        var habit = await _db.Habits
            .FirstOrDefaultAsync(h => h.Id == request.Request.HabitId && h.UserId == userId.Value, cancellationToken);

        if (habit == null)
            return Result<CheckInDto>.Failure("Habit not found", ErrorCodes.NotFound);

        if (!habit.IsActive)
            return Result<CheckInDto>.Failure("Cannot check-in to archived habit", ErrorCodes.HabitArchived);

        // Check for duplicate check-in (unique constraint: habit_id + date)
        var existingCheckIn = await _db.CheckIns
            .AnyAsync(c => c.HabitId == request.Request.HabitId && c.Date == request.Request.Date, cancellationToken);

        if (existingCheckIn)
            return Result<CheckInDto>.Failure("Check-in already exists for this habit on this date", ErrorCodes.CheckInExists);

        // Create check-in
        var checkIn = new CheckIn
        {
            Id = Guid.NewGuid(),
            HabitId = request.Request.HabitId,
            UserId = userId.Value,
            Date = request.Request.Date,
            Status = request.Request.Status,
            Note = note,
            CreatedAt = DateTime.UtcNow
        };

        _db.CheckIns.Add(checkIn);
        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqliteException { SqliteErrorCode: 19 })
        {
            // Concurrent request inserted the same (HabitId, Date) between the check above and the insert
            _db.Entry(checkIn).State = EntityState.Detached;
            return Result<CheckInDto>.Failure("Check-in already exists for this habit on this date", ErrorCodes.CheckInExists);
        }

        // Trigger score recalculation for this date
        await _mediator.Send(new Scores.RecalculateScoreCommand(request.Request.Date, userId.Value), cancellationToken);

        var response = new CheckInDto(
            checkIn.Id,
            checkIn.HabitId,
            habit.Name,
            checkIn.Date,
            checkIn.Status,
            checkIn.Note,
            checkIn.CreatedAt
        );

        return Result<CheckInDto>.Success(response);
    }
}

// Command
public record CreateCheckInCommand(CreateCheckInRequest Request) : IRequest<Result<CheckInDto>>;

// Endpoint
public static class CreateCheckInEndpoint
{
    public static IEndpointRouteBuilder MapCreateCheckIn(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/checkins", [Authorize] async (
            [FromBody] CreateCheckInRequest request,
            [FromServices] IMediator mediator) =>
        {
            var result = await mediator.Send(new CreateCheckInCommand(request));
            
            return result.IsSuccess 
                ? Results.Created($"/api/checkins/{result.Value!.Id}", result.Value)
                : result.ToErrorResult();
        })
        .WithName("CreateCheckIn")
        .WithOpenApi()
        .RequireAuthorization();

        return endpoints;
    }
}
