using System.Reflection;
using JobPlatform.GovernmentIntegration.Domain;
using JobPlatform.SharedKernel.Application.Interfaces.Cqrs;
using JobPlatform.SharedKernel.Common.Enums;
using Microsoft.Extensions.DependencyInjection;

namespace JobPlatform.GovernmentIntegration.Application;

/// <summary>Anchor for assembly scanning (handlers, validators) from the composition root.</summary>
public static class ApplicationAssembly
{
    public static Assembly Assembly => typeof(ApplicationAssembly).Assembly;

    public static IServiceCollection AddGovernmentIntegrationApplication(this IServiceCollection services)
    {
        services.AddSingleton<Events.GovernmentIntegrationEventMapper>();
        services.AddSingleton<SharedKernel.Messaging.Interfaces.IDomainEventMapper>(sp => sp.GetRequiredService<Events.GovernmentIntegrationEventMapper>());
        services.AddSingleton<GovernmentDataAccessPolicy>();
        return services;
    }
}

// ---------------------------------------------------------------------- request base types (foundation section 6/11)

/// <summary>Employer self-service command/query (POST /employer-verifications and its GET, handover section 6.1).</summary>
public abstract record EmployerCommand<TResponse> : ICommand<TResponse>, IAuthorizedRequest
{
    public IReadOnlyCollection<ActorType> AllowedActorTypes => new[] { ActorType.Employer };

    public string ForbiddenErrorCode => Domain.Common.ErrorCodes.Forbidden;
}

/// <summary>Administrator (MoL reviewer, handover Q-03) command/query: MFA required.</summary>
public abstract record AdminCommand<TResponse> : ICommand<TResponse>, IAuthorizedRequest
{
    public IReadOnlyCollection<ActorType> AllowedActorTypes => new[] { ActorType.Administrator };

    public bool RequireMfa => true;

    public string ForbiddenErrorCode => Domain.Common.ErrorCodes.Forbidden;
}

public abstract record AdminQuery<TResponse> : IQuery<TResponse>, IAuthorizedRequest
{
    public IReadOnlyCollection<ActorType> AllowedActorTypes => new[] { ActorType.Administrator };

    public bool RequireMfa => true;

    public string ForbiddenErrorCode => Domain.Common.ErrorCodes.Forbidden;
}

/// <summary>Service-to-service command/query (/internal/v1, scope gov.verify per handover section 6.1): client-credentials token, actor type System.</summary>
public abstract record ServiceCommand<TResponse> : ICommand<TResponse>, IAuthorizedRequest
{
    public IReadOnlyCollection<ActorType> AllowedActorTypes => new[] { ActorType.System };

    public string ForbiddenErrorCode => Domain.Common.ErrorCodes.AccessForbidden;
}

public abstract record ServiceQuery<TResponse> : IQuery<TResponse>, IAuthorizedRequest
{
    public IReadOnlyCollection<ActorType> AllowedActorTypes => new[] { ActorType.System };

    public string ForbiddenErrorCode => Domain.Common.ErrorCodes.AccessForbidden;
}

public static class ActorFactory
{
    public static Domain.Common.Actor From(SharedKernel.Application.Interfaces.Ports.ICurrentUser user) =>
        new(user.UserId ?? Guid.Empty, user.ActorType == ActorType.Administrator);
}

// ---------------------------------------------------------------------- anti-corruption ports (handover section 4.3: one per source system)

public enum SourceCallOutcome
{
    Match,
    NoMatch,

    /// <summary>The final outcome after the resilient client's internal 30s x 3-retry policy is exhausted (handover section 4.2).</summary>
    Unavailable
}

public sealed record EmployerVerificationCheckResult(SourceCallOutcome Outcome, string? ErrorCode);

public sealed record GovernmentDataCheckResult(SourceCallOutcome Outcome, IReadOnlyDictionary<string, string>? Fields, string? ErrorCode);

public sealed record EducationalCheckResult(SourceCallOutcome Outcome, string? ErrorCode);

public sealed record IdentityCheckResult(SourceCallOutcome Outcome, bool Ambiguous, string? ErrorCode);

public sealed record SourceSyncResult(bool Success, string? SnapshotRef, string? ErrorCode);

public sealed record LegacySourceRecord(string SourceRecordId, string RecordType, string Payload);
