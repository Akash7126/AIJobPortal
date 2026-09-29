using System.Reflection;
using JobPlatform.GovernmentIntegration.Application.DTOs.Connections;
using JobPlatform.GovernmentIntegration.Application.DTOs.EmployerVerifications;
using JobPlatform.GovernmentIntegration.Application.DTOs.Migration;
using JobPlatform.GovernmentIntegration.Application.DTOs.Verifications;
using JobPlatform.GovernmentIntegration.Domain;
using JobPlatform.SharedKernel.Application.Abstractions;
using JobPlatform.SharedKernel.Application.Paging;
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
        services.AddSingleton<SharedKernel.Messaging.IDomainEventMapper>(sp => sp.GetRequiredService<Events.GovernmentIntegrationEventMapper>());
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
    public static Domain.Common.Actor From(SharedKernel.Application.Ports.ICurrentUser user) =>
        new(user.UserId ?? Guid.Empty, user.ActorType == ActorType.Administrator);
}

/// <summary>Read side (handover section 8.3): dedicated projections, joined and paged in SQL - never via the aggregates.</summary>
public interface IGovernmentIntegrationReadStore
{
    Task<EmployerVerificationView?> GetEmployerVerificationAsync(Guid id, CancellationToken ct = default);

    Task<EmployerVerificationView?> GetActiveEmployerVerificationForEmployerAsync(Guid employerAccountId, CancellationToken ct = default);

    Task<PagedResult<EmployerVerificationView>> ListPendingManualReviewAsync(PageRequest page, CancellationToken ct = default);

    Task<SubjectVerificationStatusView> GetSubjectVerificationStatusAsync(SubjectType subjectType, Guid subjectId, CancellationToken ct = default);

    Task<IReadOnlyList<GovernmentSourceConnectionView>> ListGovernmentSourceConnectionsAsync(CancellationToken ct = default);

    Task<MigrationRunView?> GetMigrationRunAsync(Guid id, CancellationToken ct = default);

    Task<DataQualityView?> GetDataQualityByBatchAsync(Guid batchId, CancellationToken ct = default);

    Task<PagedResult<GovernmentAccessLogView>> ListGovernmentExchangesAsync(DateTime? from, DateTime? to, PageRequest page, CancellationToken ct = default);
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

/// <summary>Common shape of a "check this subject" call against a government-verification-data source (MoL, PEF or a government database).</summary>
public interface IGovernmentVerificationSourceClient
{
    Task<GovernmentDataCheckResult> CheckSubjectAsync(Subject subject, AccessPurpose purpose, CancellationToken ct);
}

public interface IMolRegistryClient : IGovernmentVerificationSourceClient
{
    Task<EmployerVerificationCheckResult> VerifyEmployerAsync(Submission submission, CancellationToken ct);

    Task<SourceSyncResult> SyncAsync(CancellationToken ct);
}

public interface IPefClient : IGovernmentVerificationSourceClient
{
    Task<SourceSyncResult> SyncAsync(CancellationToken ct);
}

public interface IGovernmentDatabaseClient : IGovernmentVerificationSourceClient
{
}

public interface IEducationalInstitutionClient
{
    Task<EducationalCheckResult> CheckAsync(Credential credential, CancellationToken ct);
}

public interface IGovernmentIdClient
{
    Task<IdentityCheckResult> CheckAsync(IdentityClaim claim, CancellationToken ct);
}

/// <summary>Legacy MoL/PEF read for the migration saga (handover section 4.3, US-6.1-02).</summary>
public interface ILegacySourceReader
{
    Task<IReadOnlyList<LegacySourceRecord>> ReadBatchAsync(SourceSystem sourceSystem, int take, CancellationToken ct);
}
