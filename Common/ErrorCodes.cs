namespace HabitSystem.Common;

/// <summary>
/// Machine-readable error codes returned in the <c>code</c> field of error responses
/// (<c>{ "error": "...", "code": "..." }</c>), and their HTTP status mapping.
/// </summary>
public static class ErrorCodes
{
    public const string Validation = "validation";                     // 400
    public const string InvalidToken = "invalid_token";                // 400 (reset / verify token)
    public const string InvalidPassword = "invalid_password";          // 400 (current password wrong)
    public const string HabitArchived = "habit_archived";              // 400
    public const string InvalidCredentials = "invalid_credentials";    // 401
    public const string Unauthorized = "unauthorized";                 // 401
    public const string InvalidRefreshToken = "invalid_refresh_token"; // 401
    public const string NotFound = "not_found";                        // 404
    public const string EmailTaken = "email_taken";                    // 409
    public const string CheckInExists = "checkin_exists";              // 409
    public const string RateLimited = "rate_limited";                  // 429
    public const string InternalError = "internal_error";              // 500

    public static int ToStatusCode(string? code) => code switch
    {
        InvalidCredentials or Unauthorized or InvalidRefreshToken => StatusCodes.Status401Unauthorized,
        NotFound => StatusCodes.Status404NotFound,
        EmailTaken or CheckInExists => StatusCodes.Status409Conflict,
        RateLimited => StatusCodes.Status429TooManyRequests,
        InternalError => StatusCodes.Status500InternalServerError,
        _ => StatusCodes.Status400BadRequest
    };
}

/// <summary>
/// Maps failed <see cref="Result"/> / <see cref="Result{T}"/> to HTTP responses with
/// a consistent body shape: <c>{ "error": string, "code": string }</c>.
/// </summary>
public static class ResultHttpExtensions
{
    public static IResult ToErrorResult(string? error, string? code)
    {
        var finalCode = code ?? ErrorCodes.Validation;
        return Results.Json(
            new { error = error ?? "Erro", code = finalCode },
            statusCode: ErrorCodes.ToStatusCode(finalCode));
    }

    public static IResult ToErrorResult(this Result result) => ToErrorResult(result.Error, result.Code);

    public static IResult ToErrorResult<T>(this Result<T> result) => ToErrorResult(result.Error, result.Code);
}
