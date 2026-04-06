using HabitSystem.Common;
using HabitSystem.Infrastructure;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HabitSystem.Features.Habits;

// Handler
public class GetHabitByIdHandler : IRequestHandler<GetHabitByIdQuery, Result<HabitDto>>
{
    private readonly AppDbContext _db;

    public GetHabitByIdHandler(AppDbContext db)
    {
        _db = db;
    }

    public async Task<Result<HabitDto>> Handle(GetHabitByIdQuery request, CancellationToken cancellationToken)
    {
        var habit = await _db.Habits
            .Where(h => h.Id == request.Id && h.UserId == Constants.DefaultUserId)
            .Select(h => new HabitDto(
                h.Id,
                h.Name,
                h.Description,
                h.Weight,
                h.PartialWeight,
                h.FrequencyType,
                h.FrequencyDays,
                h.FrequencyTimes,
                h.IsActive,
                h.CreatedAt,
                h.ArchivedAt
            ))
            .FirstOrDefaultAsync(cancellationToken);

        if (habit == null)
            return Result<HabitDto>.Failure("Habit not found");

        return Result<HabitDto>.Success(habit);
    }
}

// Query
public record GetHabitByIdQuery(Guid Id) : IRequest<Result<HabitDto>>;

// Endpoint
public static class GetHabitByIdEndpoint
{
    public static IEndpointRouteBuilder MapGetHabitById(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/habits/{id:guid}", async (
            [FromRoute] Guid id,
            [FromServices] IMediator mediator) =>
        {
            var result = await mediator.Send(new GetHabitByIdQuery(id));
            
            return result.IsSuccess 
                ? Results.Ok(result.Value)
                : Results.NotFound(new { error = result.Error });
        })
        .WithName("GetHabitById")
        .WithOpenApi();

        return endpoints;
    }
}
