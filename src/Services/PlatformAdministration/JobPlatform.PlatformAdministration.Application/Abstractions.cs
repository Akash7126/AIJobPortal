using System.Reflection;
using JobPlatform.PlatformAdministration.Domain.Common;
using JobPlatform.SharedKernel.Application.Interfaces.Cqrs;
using JobPlatform.SharedKernel.Application.Interfaces.Ports;
using JobPlatform.SharedKernel.Common.Enums;
using JobPlatform.SharedKernel.Messaging.Interfaces;
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

public sealed record ReferenceUsageResult(bool Available, IReadOnlyList<string> InUse);

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
