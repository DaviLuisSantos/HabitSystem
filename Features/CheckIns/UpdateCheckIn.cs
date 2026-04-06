using HabitSystem.Common;
using HabitSystem.Domain.Enums;
using HabitSystem.Infrastructure;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HabitSystem.Features.CheckIns;

// Request
public record UpdateCheckInRequest(
    CheckInStatus Status,
    string? Note
);

// Handler
public class UpdateCheckInHandler : IRequestHandler<UpdateCheckInCommand, Result<CheckInDto>>
{
    private readonly AppDbContext _db;
    private readonly IMediator _mediator;

    public UpdateCheckInHandler(AppDbContext db, IMediator mediator)
    {
        _db = db;
        _mediator = mediator;
    }

    public async Task<Result<CheckInDto>> Handle(UpdateCheckInCommand request, CancellationToken cancellationToken)
    {
        var checkIn = await _db.CheckIns
            .Include(c => c.Habit)
            .FirstOrDefaultAsync(c => c.Id == request.Id && c.UserId == Constants.DefaultUserId, cancellationToken);

        if (checkIn == null)
            return Result<CheckInDto>.Failure("Check-in not found");

        // Update check-in
        checkIn.Status = request.Request.Status;
        checkIn.Note = request.Request.Note;

        await _db.SaveChangesAsync(cancellationToken);

        // Trigger score recalculation for this date
        await _mediator.Send(new Scores.RecalculateScoreCommand(checkIn.Date), cancellationToken);

        var response = new CheckInDto(
            checkIn.Id,
            checkIn.HabitId,
            checkIn.Habit.Name,
            checkIn.Date,
            checkIn.Status,
            checkIn.Note,
            checkIn.CreatedAt
        );

        return Result<CheckInDto>.Success(response);
    }
}

// Command
public record UpdateCheckInCommand(Guid Id, UpdateCheckInRequest Request) : IRequest<Result<CheckInDto>>;

// Endpoint
public static class UpdateCheckInEndpoint
{
    public static IEndpointRouteBuilder MapUpdateCheckIn(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPut("/api/checkins/{id:guid}", async (
            [FromRoute] Guid id,
            [FromBody] UpdateCheckInRequest request,
            [FromServices] IMediator mediator) =>
        {
            var result = await mediator.Send(new UpdateCheckInCommand(id, request));
            
            return result.IsSuccess 
                ? Results.Ok(result.Value)
                : Results.BadRequest(new { error = result.Error });
        })
        .WithName("UpdateCheckIn")
        .WithOpenApi();

        return endpoints;
    }
}
