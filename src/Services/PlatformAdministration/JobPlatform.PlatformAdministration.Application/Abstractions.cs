using System.Reflection;
using JobPlatform.PlatformAdministration.Application.DTOs.Entities;
using JobPlatform.PlatformAdministration.Application.DTOs.Offerings;
using JobPlatform.PlatformAdministration.Application.DTOs.Reference;
using JobPlatform.PlatformAdministration.Application.DTOs.Settings;
using JobPlatform.PlatformAdministration.Application.DTOs.Taxonomy;
using JobPlatform.PlatformAdministration.Application.DTOs.Users;
using JobPlatform.PlatformAdministration.Domain.Common;
using JobPlatform.SharedKernel.Application.Abstractions;
using JobPlatform.SharedKernel.Application.Paging;
using JobPlatform.SharedKernel.Application.Ports;
using JobPlatform.SharedKernel.Common.Enums;
using JobPlatform.SharedKernel.Messaging;
using Microsoft.Extensions.DependencyInjection;

namespace JobPlatform.PlatformAdministration.Application;

/// <summary>Anchor for assembly scanning (handlers, validators) from the composition root.</summary>
public static class ApplicationAssembly
{
    public static Assembly Assembly => typeof(ApplicationAssembly).Assembly;

    public static IServiceCollection AddPlatformAdministrationApplication(this IServiceCollection services)
    {
        services.AddSingleton<Events.PlatformAdministrationEventMapper>();
        services.AddSingleton<IDomainEventMapper>(sp => sp.GetRequiredService<Events.PlatformAdministrationEventMapper>());
        services.AddScoped<Taxonomy.TaxonomyReader>();
        services.AddScoped<Reference.ReferenceFileReader>();
        return services;
    }
}

/// <summary>Command whose concurrent saves follow "later save wins" (US-3.1.4-06/07/08 AC-03): a lost optimistic race is retried on fresh state.</summary>
public interface ILaterSaveWinsCommand
{
}

/// <summary>Administrator command: Administrator actor type with MFA, refused with E-AUM-FORBIDDEN.</summary>
public abstract record AdminCommand<TResponse> : ICommand<TResponse>, IAuthorizedRequest
{
    public IReadOnlyCollection<ActorType> AllowedActorTypes => new[] { ActorType.Administrator };

    public bool RequireMfa => true;

    public string ForbiddenErrorCode => ErrorCodes.Forbidden;
}

/// <summary>Administrator query: Administrator actor type with MFA, refused with E-AUM-FORBIDDEN.</summary>
public abstract record AdminQuery<TResponse> : IQuery<TResponse>, IAuthorizedRequest
{
    public IReadOnlyCollection<ActorType> AllowedActorTypes => new[] { ActorType.Administrator };

    public bool RequireMfa => true;

    public string ForbiddenErrorCode => ErrorCodes.Forbidden;
}

/// <summary>Service-to-service query (/internal/v1): client-credentials token (actor type System).</summary>
public abstract record ServiceQuery<TResponse> : IQuery<TResponse>, IAuthorizedRequest
{
    public IReadOnlyCollection<ActorType> AllowedActorTypes => new[] { ActorType.System };
}

public static class ActorFactory
{
    public static Actor From(ICurrentUser user) => new(user.UserId ?? Guid.Empty, user.ActorType == ActorType.Administrator);
}

// ---------------------------------------------------------------------- ports

/// <summary>Read side (foundation section 3.5): dedicated projections, never aggregates.</summary>
public interface IAdminReadStore
{
    Task<IReadOnlyList<SettingView>> ListSettingsAsync(CancellationToken ct = default);

    Task<SettingView?> GetSettingAsync(string key, CancellationToken ct = default);

    Task<ReferenceFileView?> GetReferenceFileAsync(string type, CancellationToken ct = default);

    /// <summary>Current version of a taxonomy, or null when it does not exist.</summary>
    Task<int?> GetTaxonomyCurrentVersionAsync(string type, CancellationToken ct = default);

    /// <summary>The taxonomy at a version (null = current). Null when the taxonomy or the version does not exist.</summary>
    Task<TaxonomyView?> GetTaxonomyAsync(string type, int? version, CancellationToken ct = default);

    Task<PagedResult<JobOfferingListItem>> ListJobOfferingsAsync(string? status, PageRequest page, CancellationToken ct = default);

    Task<EntityRecordView?> GetEntityRecordAsync(Guid id, CancellationToken ct = default);
}

/// <summary>Anti-corruption port to the user directory of BC-03 (US-3.1.4-01, Q-06: live composition). Adapter chosen by UserDirectory:Provider.</summary>
public interface IUserDirectory
{
    Task<JobPlatform.SharedKernel.Application.Results.Result<PagedResult<PlatformUserListItem>>> ListAsync(
        string? type, string? status, string? search, PageRequest page, CancellationToken ct = default);
}

public sealed record ReferenceUsageResult(bool Available, IReadOnlyList<string> InUse);

/// <summary>Port to the owners of references (BC-04/09/10): which of these codes are still referenced? Adapter chosen by ReferenceUsage:Provider.</summary>
public interface IReferenceUsageChecker
{
    Task<ReferenceUsageResult> CheckAsync(string referenceType, IReadOnlyCollection<string> codes, CancellationToken ct = default);
}

/// <summary>Cache-aside store for reference data (foundation section 10). Failures degrade to the database, never to an error.</summary>
public interface IReferenceDataCache
{
    Task<T?> GetAsync<T>(string key, CancellationToken ct = default);

    Task SetAsync<T>(string key, T value, TimeSpan ttl, CancellationToken ct = default);

    Task RemoveAsync(string key, CancellationToken ct = default);
}

/// <summary>Key names and TTLs of handover section 9 (the store adds the "&lt;env&gt;:platform-administration" prefix).</summary>
public static class CacheKeys
{
    public static readonly TimeSpan TaxonomyVersionTtl = TimeSpan.FromMinutes(60);
    public static readonly TimeSpan TaxonomyCurrentTtl = TimeSpan.FromMinutes(5);
    public static readonly TimeSpan ReferenceTtl = TimeSpan.FromMinutes(30);
    public static readonly TimeSpan SettingTtl = TimeSpan.FromMinutes(5);

    public static string Taxonomy(string type, int version) => $"taxonomy:{type}:v{version}";

    public static string TaxonomyCurrent(string type) => $"taxonomy:{type}:current";

    public static string Reference(string type) => $"reference:{type}";

    public static string Setting(string key) => $"setting:{key}";
}
