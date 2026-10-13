namespace HabitSystem.Common;

/// <summary>
/// Represents the result of an operation that can succeed or fail
/// </summary>
public class Result<T>
{
    public bool IsSuccess { get; }
    public T? Value { get; }
    public string? Error { get; }

    /// <summary>
    /// Machine-readable error code (see <see cref="ErrorCodes"/>). Null on success.
    /// </summary>
    public string? Code { get; }

    private Result(bool isSuccess, T? value, string? error, string? code)
    {
        IsSuccess = isSuccess;
        Value = value;
        Error = error;
        Code = code;
    }

    public static Result<T> Success(T value) => new(true, value, null, null);
    public static Result<T> Failure(string error, string code = ErrorCodes.Validation) => new(false, default, error, code);
}

/// <summary>
/// Represents the result of an operation without a return value
/// </summary>
public class Result
{
    public bool IsSuccess { get; }
    public string? Error { get; }

    /// <summary>
    /// Machine-readable error code (see <see cref="ErrorCodes"/>). Null on success.
    /// </summary>
    public string? Code { get; }

    private Result(bool isSuccess, string? error, string? code)
    {
        IsSuccess = isSuccess;
        Error = error;
        Code = code;
    }

    public static Result Success() => new(true, null, null);
    public static Result Failure(string error, string code = ErrorCodes.Validation) => new(false, error, code);
}
