using System.Security.Claims;

namespace HabitSystem.Common;

/// <summary>
/// Extension methods for ClaimsPrincipal to extract user information
/// </summary>
public static class ClaimsPrincipalExtensions
{
    /// <summary>
    /// Gets the user ID from the ClaimsPrincipal
    /// </summary>
    public static Guid? GetUserId(this ClaimsPrincipal principal)
    {
        var userIdClaim = principal.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier);
        if (userIdClaim != null && Guid.TryParse(userIdClaim.Value, out var userId))
        {
            return userId;
        }
        return null;
    }

    /// <summary>
    /// Gets the user email from the ClaimsPrincipal
    /// </summary>
    public static string? GetUserEmail(this ClaimsPrincipal principal)
    {
        return principal.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Email)?.Value;
    }
}
