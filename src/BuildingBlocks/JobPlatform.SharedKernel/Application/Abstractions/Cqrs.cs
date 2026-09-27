using JobPlatform.SharedKernel.Application.Results;
using JobPlatform.SharedKernel.Common.Enums;
using JobPlatform.SharedKernel.Domain;

namespace JobPlatform.SharedKernel.Application.Abstractions;

public interface IRequest<TResponse>
{
}

/// <summary>Marker for commands (state-changing requests) - used by the unit-of-work behavior.</summary>
public interface ICommandBase
{
}

public interface ICommand<TResponse> : IRequest<TResponse>, ICommandBase
{
}

public interface ICommand : ICommand<Unit>
{
}

public interface IQuery<TResponse> : IRequest<TResponse>
{
}

public interface IRequestHandler<in TRequest, TResponse> where TRequest : IRequest<TResponse>
{
    Task<Result<TResponse>> Handle(TRequest request, CancellationToken ct);
}

public interface ICommandHandler<in TCommand, TResponse> : IRequestHandler<TCommand, TResponse>
    where TCommand : ICommand<TResponse>
{
}

public interface IQueryHandler<in TQuery, TResponse> : IRequestHandler<TQuery, TResponse>
    where TQuery : IQuery<TResponse>
{
}

public delegate Task<Result<TResponse>> RequestHandlerDelegate<TResponse>();

/// <summary>Cross-cutting step around a handler. First registered = outermost.</summary>
public interface IPipelineBehavior<in TRequest, TResponse> where TRequest : IRequest<TResponse>
{
    Task<Result<TResponse>> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct);
}

public interface ISender
{
    Task<Result<TResponse>> Send<TResponse>(IRequest<TResponse> request, CancellationToken ct = default);
}

/// <summary>Command that supports the Idempotency-Key header.</summary>
public interface IIdempotentCommand
{
    string? IdempotencyKey { get; }
}

/// <summary>Request that requires an authenticated caller and (optionally) roles/permission. Requests without it are anonymous.</summary>
public interface IAuthorizedRequest
{
    /// <summary>Empty = any authenticated actor type.</summary>
    IReadOnlyCollection<ActorType> AllowedActorTypes => Array.Empty<ActorType>();

    string? RequiredPermission => null;

    bool RequireMfa => false;

    /// <summary>External error code returned when access is refused.</summary>
    string ForbiddenErrorCode => "E-AAFR-FORBIDDEN";
}

/// <summary>Request throttled per source (registration, activation, ...). Every call counts as an attempt.</summary>
public interface IRateLimitedRequest
{
    string RateLimitScope { get; }
    string RateLimitedErrorCode { get; }
    int RateLimitPermits => 5;
    TimeSpan RateLimitWindow => TimeSpan.FromMinutes(15);

    /// <summary>Extra discriminator appended to the caller source (e.g. account id). Null = source only.</summary>
    string? RateLimitDiscriminator => null;
}

/// <summary>Command whose state changes (failed-attempt counters, lockouts) must be committed even when the result is a failure.</summary>
public interface IPersistOnFailure
{
}

/// <summary>Command that can hit a unique-index violation; supplies the error code for that case.</summary>
public interface IConflictAwareCommand
{
    string UniqueViolationErrorCode { get; }
}

public interface IDomainEventHandler<in TEvent> where TEvent : IDomainEvent
{
    Task Handle(TEvent domainEvent, CancellationToken ct);
}
