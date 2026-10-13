namespace PersoNet.Application.Common;

public enum ErrorType
{
    Validation,
    NotFound,
    Conflict,
    Forbidden,
}

/// <summary>
/// An expected failure of a use case. <see cref="Code"/> is stable, so clients can show their own message.
/// </summary>
public sealed record AppError(ErrorType Type, string Code, string Message)
{
    public static AppError Validation(string code, string message) => new(ErrorType.Validation, code, message);

    public static AppError NotFound(string code, string message) => new(ErrorType.NotFound, code, message);

    public static AppError Conflict(string code, string message) => new(ErrorType.Conflict, code, message);

    public static AppError Forbidden(string code, string message) => new(ErrorType.Forbidden, code, message);
}

/// <summary>
/// Outcome of a use case: either success or an <see cref="Error"/>. Exceptions are left for bugs and outages.
/// </summary>
public class Result
{
    protected Result(AppError? error)
    {
        Error = error;
    }

    public AppError? Error { get; }

    public bool IsSuccess => Error is null;

    public static Result Success() => new(null);

    public static Result Failure(AppError error) => new(error ?? throw new ArgumentNullException(nameof(error)));

#pragma warning disable CA2225 // Implicit conversions keep handlers readable; Failure covers the named alternative.
    public static implicit operator Result(AppError error) => Failure(error);
#pragma warning restore CA2225
}

public sealed class Result<T> : Result
{
    private readonly T? _value;

    private Result(T value)
        : base(null)
    {
        _value = value;
    }

    private Result(AppError error)
        : base(error)
    {
    }

    public T Value => IsSuccess ? _value! : throw new InvalidOperationException("A failed result has no value.");

#pragma warning disable CA2225 // See Result.
    public static implicit operator Result<T>(T value) => new(value);

    public static implicit operator Result<T>(AppError error) => new(error ?? throw new ArgumentNullException(nameof(error)));
#pragma warning restore CA2225
}
