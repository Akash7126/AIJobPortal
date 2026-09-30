using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using FluentValidation;
using JobPlatform.BuildingBlocks.Infrastructure.Diagnostics;
using JobPlatform.BuildingBlocks.Infrastructure.Interfaces.Persistence;
using JobPlatform.BuildingBlocks.Infrastructure.Persistence;
using JobPlatform.SharedKernel.Application.Abstractions;
using JobPlatform.SharedKernel.Application.Interfaces.Cqrs;
using JobPlatform.SharedKernel.Application.Interfaces.Persistence;
using JobPlatform.SharedKernel.Application.Interfaces.Ports;
using JobPlatform.SharedKernel.Application.Persistence;
using JobPlatform.SharedKernel.Application.Ports;
using JobPlatform.SharedKernel.Application.Results;
using JobPlatform.SharedKernel.Domain;
using Microsoft.Extensions.Logging;

namespace JobPlatform.BuildingBlocks.Infrastructure.Behaviors;

/// <summary>1. Structured log + timing. Logs the request type and outcome code only - never the payload (it may hold secrets).</summary>
public sealed class LoggingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse> where TRequest : IRequest<TResponse>
{
    private readonly ILogger<LoggingBehavior<TRequest, TResponse>> _logger;
    private readonly ICorrelationContext _correlation;

    public LoggingBehavior(ILogger<LoggingBehavior<TRequest, TResponse>> logger, ICorrelationContext correlation)
    {
        _logger = logger;
        _correlation = correlation;
    }

    public async Task<Result<TResponse>> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
    {
        var name = typeof(TRequest).Name;
        var stopwatch = Stopwatch.StartNew();
        using var activity = BuildingBlockTelemetry.ActivitySource.StartActivity("handle " + name);
        var outcome = "success";
        try
        {
            var result = await next();
            stopwatch.Stop();
            if (result.IsSuccess)
            {
                _logger.LogInformation("Handled {Request} in {ElapsedMs} ms (correlation {CorrelationId})", name, stopwatch.ElapsedMilliseconds, _correlation.CorrelationId);
            }
            else
            {
                outcome = result.Error!.Type.ToString();
                activity?.SetTag("error.code", result.Error.Code);
                _logger.LogInformation("Rejected {Request} with {ErrorCode} ({ErrorType}) in {ElapsedMs} ms (correlation {CorrelationId})",
                    name, result.Error!.Code, result.Error.Type, stopwatch.ElapsedMilliseconds, _correlation.CorrelationId);
            }

            return result;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            stopwatch.Stop();
            outcome = "exception";
            activity?.SetStatus(ActivityStatusCode.Error);
            _logger.LogError(ex, "Unhandled exception in {Request} after {ElapsedMs} ms (correlation {CorrelationId})", name, stopwatch.ElapsedMilliseconds, _correlation.CorrelationId);
            throw;
        }
        finally
        {
            BuildingBlockTelemetry.HandlerDuration.Record(stopwatch.Elapsed.TotalMilliseconds,
                new KeyValuePair<string, object?>("request", name), new KeyValuePair<string, object?>("outcome", outcome));
        }
    }
}

/// <summary>2. Throttles per caller source before any work is done (registration, activation, ...). Every call is an attempt.</summary>
public sealed class RateLimitBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse> where TRequest : IRequest<TResponse>
{
    private readonly IRateLimiter _limiter;
    private readonly ICurrentUser _user;

    public RateLimitBehavior(IRateLimiter limiter, ICurrentUser user)
    {
        _limiter = limiter;
        _user = user;
    }

    public async Task<Result<TResponse>> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
    {
        if (request is not IRateLimitedRequest limited)
        {
            return await next();
        }

        var key = $"{limited.RateLimitScope}:{_user.SourceKey}" + (limited.RateLimitDiscriminator is { } d ? $":{d}" : string.Empty);
        var decision = await _limiter.HitAsync(key, limited.RateLimitPermits, limited.RateLimitWindow, ct);
        if (!decision.Allowed)
        {
            return Error.TooManyRequests(limited.RateLimitedErrorCode, "Too many attempts. Try again later.", decision.RetryAfter);
        }

        return await next();
    }
}

/// <summary>3. Runs every FluentValidation validator before the domain is touched (malformed input = 400, never a domain exception).</summary>
public sealed class ValidationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse> where TRequest : IRequest<TResponse>
{
    private readonly IEnumerable<IValidator<TRequest>> _validators;

    public ValidationBehavior(IEnumerable<IValidator<TRequest>> validators) => _validators = validators;

    public async Task<Result<TResponse>> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
    {
        if (!_validators.Any())
        {
            return await next();
        }

        var context = new ValidationContext<TRequest>(request);
        var failures = new List<FluentValidation.Results.ValidationFailure>();
        foreach (var validator in _validators)
        {
            var result = await validator.ValidateAsync(context, ct);
            failures.AddRange(result.Errors);
        }

        if (failures.Count == 0)
        {
            return await next();
        }

        var errors = failures
            .GroupBy(f => ToCamelCase(f.PropertyName))
            .ToDictionary(g => g.Key, g => g.Select(f => f.ErrorCode).Distinct().ToArray());
        return Error.Validation(errors);
    }

    private static string ToCamelCase(string path) =>
        string.Join('.', path.Split('.').Select(s => s.Length == 0 ? s : char.ToLowerInvariant(s[0]) + s[1..]));
}

/// <summary>4. RBAC / actor-type / MFA check (THR-031). Requests that do not implement <see cref="IAuthorizedRequest"/> are anonymous.</summary>
public sealed class AuthorizationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse> where TRequest : IRequest<TResponse>
{
    private readonly IAccessAuthorizer _authorizer;
    private readonly ICurrentUser _user;

    public AuthorizationBehavior(IAccessAuthorizer authorizer, ICurrentUser user)
    {
        _authorizer = authorizer;
        _user = user;
    }

    public async Task<Result<TResponse>> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
    {
        if (request is not IAuthorizedRequest authorized)
        {
            return await next();
        }

        var decision = await _authorizer.AuthorizeAsync(_user, authorized, typeof(TRequest).Name, ct);
        if (decision.IsFailure)
        {
            return decision.Error!;
        }

        return await next();
    }
}

/// <summary>5. Replays the stored response for a repeated Idempotency-Key; rejects reuse of a key with a different payload.</summary>
public sealed class IdempotencyBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse> where TRequest : IRequest<TResponse>
{
    public static readonly TimeSpan Retention = TimeSpan.FromHours(24);

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    private readonly IIdempotencyStore _store;
    private readonly ICurrentUser _user;

    public IdempotencyBehavior(IIdempotencyStore store, ICurrentUser user)
    {
        _store = store;
        _user = user;
    }

    public async Task<Result<TResponse>> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
    {
        if (request is not IIdempotentCommand { IdempotencyKey: { Length: > 0 } key })
        {
            return await next();
        }

        var scope = $"{typeof(TRequest).Name}:{_user.UserId?.ToString() ?? "anonymous"}";
        var fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(request, request.GetType(), Json))));

        if (!await _store.TryBeginAsync(scope, key, fingerprint, Retention, ct))
        {
            var existing = await _store.GetAsync(scope, key, ct);
            if (existing is null)
            {
                return await next();
            }

            if (existing.Fingerprint != fingerprint)
            {
                return Error.BusinessRule("E-IDEMPOTENCY-KEY-REUSED", "The Idempotency-Key was already used with a different request.");
            }

            if (!existing.Completed)
            {
                return Error.Conflict("E-IDEMPOTENCY-IN-PROGRESS", "A request with this Idempotency-Key is still being processed.");
            }

            return existing.IsSuccess
                ? Result.Success(JsonSerializer.Deserialize<TResponse>(existing.PayloadJson!, Json)!)
                : Error.Unexpected("E-IDEMPOTENCY-CORRUPT", "The stored response could not be replayed.");
        }

        try
        {
            var result = await next();
            if (result.IsSuccess)
            {
                await _store.CompleteAsync(scope, key,
                    new IdempotencyEntry(fingerprint, true, true, JsonSerializer.Serialize(result.Value, Json), null), Retention, ct);
            }
            else
            {
                // Failures are not cached: the client may correct the request and retry with the same key.
                await _store.ReleaseAsync(scope, key, ct);
            }

            return result;
        }
        catch
        {
            await _store.ReleaseAsync(scope, key, CancellationToken.None);
            throw;
        }
    }
}

/// <summary>
/// 6. One transaction per command: handler, SaveChanges (including outbox rows for raised domain events), commit.
/// Domain events are dispatched to in-process handlers after the commit. Queries pass straight through.
/// </summary>
public sealed class UnitOfWorkBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse> where TRequest : IRequest<TResponse>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly DomainEventBuffer _events;
    private readonly IDomainEventDispatcher _dispatcher;

    public UnitOfWorkBehavior(IUnitOfWork unitOfWork, DomainEventBuffer events, IDomainEventDispatcher dispatcher)
    {
        _unitOfWork = unitOfWork;
        _events = events;
        _dispatcher = dispatcher;
    }

    public async Task<Result<TResponse>> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
    {
        if (request is not ICommandBase)
        {
            return await next();
        }

        await _unitOfWork.BeginTransactionAsync(ct);
        try
        {
            var result = await next();
            if (result.IsFailure && request is not IPersistOnFailure)
            {
                await _unitOfWork.RollbackTransactionAsync(ct);
                return result;
            }

            await _unitOfWork.SaveChangesAsync(ct);
            await _unitOfWork.CommitTransactionAsync(ct);
            await _dispatcher.DispatchAsync(_events.Drain(), ct);
            return result;
        }
        catch (BusinessRuleViolationException ex)
        {
            await _unitOfWork.RollbackTransactionAsync(CancellationToken.None);
            return ex.ToError();
        }
        catch (ConcurrencyConflictException)
        {
            await _unitOfWork.RollbackTransactionAsync(CancellationToken.None);
            return Error.Conflict("E-CONCURRENCY-CONFLICT", "The resource was modified by another request. Reload and try again.");
        }
        catch (UniqueConstraintViolationException)
        {
            await _unitOfWork.RollbackTransactionAsync(CancellationToken.None);
            var code = request is IConflictAwareCommand aware ? aware.UniqueViolationErrorCode : "E-DUPLICATE";
            return Error.Conflict(code, "A record with the same unique values already exists.");
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync(CancellationToken.None);
            throw;
        }
    }
}
