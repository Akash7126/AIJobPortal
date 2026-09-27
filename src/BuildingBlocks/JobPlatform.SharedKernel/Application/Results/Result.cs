namespace JobPlatform.SharedKernel.Application.Results;

public enum ErrorType
{
    Validation,
    NotFound,
    Conflict,
    BusinessRule,
    Unauthorized,
    Forbidden,
    TooManyRequests,
    External,
    Unexpected,
    PreconditionFailed
}

/// <summary>Expected failure. <see cref="Code"/> is the stable, externally published code (e.g. E-JSRPM-DUPLICATE).</summary>
public sealed record Error(string Code, string Message, ErrorType Type)
{
    /// <summary>Internal domain rule code (AI.Account.DUPLICATE) when the error came from a business rule.</summary>
    public string? RuleCode { get; init; }

    /// <summary>Field to error codes, for validation failures (foundation section 7).</summary>
    public IReadOnlyDictionary<string, string[]>? ValidationErrors { get; init; }

    public TimeSpan? RetryAfter { get; init; }

    public static Error Validation(IReadOnlyDictionary<string, string[]> errors, string code = "VAL.INVALID_REQUEST", string message = "One or more validation errors occurred.") =>
        new(code, message, ErrorType.Validation) { ValidationErrors = errors };

    public static Error NotFound(string code, string message) => new(code, message, ErrorType.NotFound);
    public static Error Conflict(string code, string message) => new(code, message, ErrorType.Conflict);
    public static Error BusinessRule(string code, string message) => new(code, message, ErrorType.BusinessRule);
    public static Error Unauthorized(string code, string message) => new(code, message, ErrorType.Unauthorized);
    public static Error Forbidden(string code, string message) => new(code, message, ErrorType.Forbidden);
    public static Error TooManyRequests(string code, string message, TimeSpan? retryAfter = null) =>
        new(code, message, ErrorType.TooManyRequests) { RetryAfter = retryAfter };
    public static Error External(string code, string message) => new(code, message, ErrorType.External);
    public static Error Unexpected(string code, string message) => new(code, message, ErrorType.Unexpected);

    /// <summary>The If-Match precondition no longer holds (the resource changed since the client read it): HTTP 412.</summary>
    public static Error PreconditionFailed(string code, string message) => new(code, message, ErrorType.PreconditionFailed);
}

public readonly record struct Unit
{
    public static readonly Unit Value = default;
}

public class Result
{
    protected Result(bool isSuccess, Error? error)
    {
        IsSuccess = isSuccess;
        Error = error;
    }

    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;
    public Error? Error { get; }

    public static Result<Unit> Success() => new(Unit.Value);
    public static Result<T> Success<T>(T value) => new(value);
    public static Result<T> Failure<T>(Error error) => new(error);
}

public sealed class Result<T> : Result
{
    private readonly T? _value;

    internal Result(T value) : base(true, null) => _value = value;

    internal Result(Error error) : base(false, error) { }

    public T Value => IsSuccess ? _value! : throw new InvalidOperationException("Cannot read the value of a failed result.");

    public static Result<T> Failure(Error error) => new(error);

    public static implicit operator Result<T>(T value) => new(value);

    public static implicit operator Result<T>(Error error) => new(error);

    public TOut Match<TOut>(Func<T, TOut> onSuccess, Func<Error, TOut> onFailure) =>
        IsSuccess ? onSuccess(_value!) : onFailure(Error!);
}
