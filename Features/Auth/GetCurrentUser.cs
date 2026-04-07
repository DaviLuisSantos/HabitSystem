using HabitSystem.Common;
using HabitSystem.Infrastructure;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;

namespace HabitSystem.Features.Auth;

/// <summary>
/// Get current authenticated user endpoint
/// </summary>
public static class GetCurrentUser
{
    public record Query : IRequest<Result<Response>>;

    public record Response(Guid UserId, string Email, string Name, string Timezone, DateTime CreatedAt);

    public class Handler : IRequestHandler<Query, Result<Response>>
    {
        private readonly AppDbContext _context;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public Handler(AppDbContext context, IHttpContextAccessor httpContextAccessor)
        {
            _context = context;
            _httpContextAccessor = httpContextAccessor;
        }

        public async Task<Result<Response>> Handle(Query request, CancellationToken cancellationToken)
        {
            var httpContext = _httpContextAccessor.HttpContext;
            if (httpContext == null)
                return Result<Response>.Failure("No HTTP context available");

            // Get user ID from claims
            var userIdClaim = httpContext.User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
                return Result<Response>.Failure("User not authenticated");

            // Find user
            var user = await _context.Users.FindAsync(new object[] { userId }, cancellationToken);
            if (user == null)
                return Result<Response>.Failure("User not found");

            return Result<Response>.Success(new Response(
                user.Id,
                user.Email,
                user.Name,
                user.Timezone,
                user.CreatedAt
            ));
        }
    }

    public static void MapGetCurrentUserEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/auth/me", [Authorize] async (IMediator mediator) =>
        {
            var result = await mediator.Send(new Query());
            return result.IsSuccess
                ? Results.Ok(result.Value)
                : Results.BadRequest(new { error = result.Error });
        })
        .WithName("GetCurrentUser")
        .WithOpenApi()
        .RequireAuthorization();
    }
}
