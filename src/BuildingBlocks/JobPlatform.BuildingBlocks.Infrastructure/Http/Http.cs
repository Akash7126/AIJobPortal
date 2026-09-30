using JobPlatform.SharedKernel.Application.Interfaces.Ports;
using JobPlatform.SharedKernel.Application.Persistence;
using JobPlatform.SharedKernel.Application.Results;
using JobPlatform.SharedKernel.Common.Enums;
using JobPlatform.SharedKernel.Domain;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace JobPlatform.BuildingBlocks.Infrastructure.Http;

public sealed class CorrelationContext : ICorrelationContext
{
    public Guid CorrelationId { get; private set; } = Guid.NewGuid();
    public Guid? CausationId { get; private set; }

    public void Set(Guid correlationId, Guid? causationId = null)
    {
        CorrelationId = correlationId;
        CausationId = causationId;
    }
}

/// <summary>Accepts or creates X-Correlation-Id, echoes it on the response and adds it to the log scope. Copied onto every outbox message.</summary>
public sealed class CorrelationIdMiddleware
{
    public const string HeaderName = "X-Correlation-Id";

    private readonly RequestDelegate _next;
    private readonly ILogger<CorrelationIdMiddleware> _logger;

    public CorrelationIdMiddleware(RequestDelegate next, ILogger<CorrelationIdMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, CorrelationContext correlation)
    {
        var incoming = context.Request.Headers[HeaderName].FirstOrDefault();
        var id = Guid.TryParse(incoming, out var parsed) ? parsed : Guid.NewGuid();
        correlation.Set(id);
        context.Response.Headers[HeaderName] = id.ToString();

        using (_logger.BeginScope(new Dictionary<string, object> { ["CorrelationId"] = id }))
        {
            await _next(context);
        }
    }
}

/// <summary>Maps <see cref="Error"/> to RFC 9457 problem details (foundation sections 7 and 11): code, localised detail, errors, traceId.</summary>
public static class ProblemDetailsMapper
{
    public static int StatusFor(ErrorType type) => type switch
    {
        ErrorType.Validation => StatusCodes.Status400BadRequest,
        ErrorType.NotFound => StatusCodes.Status404NotFound,
        ErrorType.Conflict => StatusCodes.Status409Conflict,
        ErrorType.BusinessRule => StatusCodes.Status422UnprocessableEntity,
        ErrorType.Unauthorized => StatusCodes.Status401Unauthorized,
        ErrorType.Forbidden => StatusCodes.Status403Forbidden,
        ErrorType.TooManyRequests => StatusCodes.Status429TooManyRequests,
        ErrorType.External => StatusCodes.Status502BadGateway,
        ErrorType.PreconditionFailed => StatusCodes.Status412PreconditionFailed,
        _ => StatusCodes.Status500InternalServerError
    };

    public static ProblemDetails Create(Error error, HttpContext context)
    {
        var status = StatusFor(error.Type);
        var language = context.RequestServices.GetService<ICurrentUser>()?.Language ?? Language.En;
        var localizer = context.RequestServices.GetService<IErrorMessageLocalizer>();
        var detail = localizer?.Localize(error.Code, error.Message, language) ?? error.Message;

        var problem = new ProblemDetails
        {
            Type = $"https://errors.jobplatform.example/{error.Code}",
            Title = TitleFor(status),
            Status = status,
            Detail = status >= 500 ? "An unexpected error occurred." : detail,
            Instance = context.Request.Path
        };
        problem.Extensions["code"] = error.Code;
        problem.Extensions["traceId"] = context.TraceIdentifier;
        var correlation = context.RequestServices.GetService<ICorrelationContext>();
        if (correlation is not null)
        {
            problem.Extensions["correlationId"] = correlation.CorrelationId;
        }

        if (error.RuleCode is not null)
        {
            problem.Extensions["ruleCode"] = error.RuleCode;
        }

        if (error.ValidationErrors is not null)
        {
            problem.Extensions["errors"] = error.ValidationErrors;
        }

        return problem;
    }

    public static IActionResult ToActionResult(this Error error, HttpContext context)
    {
        var problem = Create(error, context);
        if (error.RetryAfter is { } retry)
        {
            context.Response.Headers.RetryAfter = ((int)Math.Ceiling(retry.TotalSeconds)).ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        if (error.Type == ErrorType.Unauthorized)
        {
            context.Response.Headers.WWWAuthenticate = "Bearer";
        }

        return new ObjectResult(problem) { StatusCode = problem.Status, ContentTypes = { "application/problem+json" } };
    }

    private static string TitleFor(int status) => status switch
    {
        400 => "Bad Request",
        401 => "Unauthorized",
        403 => "Forbidden",
        404 => "Not Found",
        409 => "Conflict",
        412 => "Precondition Failed",
        422 => "Unprocessable Entity",
        429 => "Too Many Requests",
        502 => "Bad Gateway",
        _ => "Internal Server Error"
    };
}

public static class ResultHttpExtensions
{
    /// <summary>Writes the error as application/problem+json directly to the response (for middleware/auth handlers outside MVC).</summary>
    public static async Task WriteProblemAsync(this HttpContext context, Error error, CancellationToken ct = default)
    {
        var problem = ProblemDetailsMapper.Create(error, context);
        context.Response.StatusCode = problem.Status ?? StatusCodes.Status500InternalServerError;
        if (error.RetryAfter is { } retry)
        {
            context.Response.Headers.RetryAfter = ((int)Math.Ceiling(retry.TotalSeconds)).ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        if (error.Type == ErrorType.Unauthorized)
        {
            context.Response.Headers.WWWAuthenticate = "Bearer";
        }

        await context.Response.WriteAsJsonAsync(problem, options: null, contentType: "application/problem+json", cancellationToken: ct);
    }

    /// <summary>Success runs the mapping; failure becomes problem+json. Keeps controllers thin.</summary>
    public static IActionResult ToActionResult<T>(this Result<T> result, HttpContext context, Func<T, IActionResult> onSuccess) =>
        result.IsSuccess ? onSuccess(result.Value) : result.Error!.ToActionResult(context);
}

/// <summary>Last-resort mapping so a rule violation or persistence conflict that escapes a handler still returns the documented status.</summary>
public sealed class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;

    public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) => _logger = logger;

    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        Error error;
        switch (exception)
        {
            case BusinessRuleViolationException rule:
                error = rule.ToError();
                break;
            case ConcurrencyConflictException:
                error = Error.Conflict("E-CONCURRENCY-CONFLICT", "The resource was modified by another request.");
                break;
            case UniqueConstraintViolationException:
                error = Error.Conflict("E-DUPLICATE", "A record with the same unique values already exists.");
                break;
            case OperationCanceledException when httpContext.RequestAborted.IsCancellationRequested:
                return true;
            default:
                _logger.LogError(exception, "Unhandled exception (trace {TraceId})", httpContext.TraceIdentifier);
                error = Error.Unexpected("E-UNEXPECTED", "An unexpected error occurred.");
                break;
        }

        var result = error.ToActionResult(httpContext);
        var problem = (ProblemDetails)((ObjectResult)result).Value!;
        httpContext.Response.StatusCode = problem.Status ?? 500;
        httpContext.Response.ContentType = "application/problem+json";
        await httpContext.Response.WriteAsJsonAsync(problem, options: null, contentType: "application/problem+json", cancellationToken);
        return true;
    }
}
