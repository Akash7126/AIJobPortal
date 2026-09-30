using System.Reflection;
using JobPlatform.ExternalIntegration.Application.Services.JobDataFlows;
using JobPlatform.ExternalIntegration.Domain;
using JobPlatform.SharedKernel.Application.Interfaces.Cqrs;
using JobPlatform.SharedKernel.Application.Interfaces.Ports;
using JobPlatform.SharedKernel.Common.Enums;
using Microsoft.Extensions.DependencyInjection;

namespace JobPlatform.ExternalIntegration.Application;

/// <summary>Anchor for assembly scanning (handlers, validators) from the composition root.</summary>
public static class ApplicationAssembly
{
    public static Assembly Assembly => typeof(ApplicationAssembly).Assembly;

    public static IServiceCollection AddExternalIntegrationApplication(this IServiceCollection services)
    {
        services.AddSingleton<Events.ExternalIntegrationEventMapper>();
        services.AddSingleton<SharedKernel.Messaging.Interfaces.IDomainEventMapper>(sp => sp.GetRequiredService<Events.ExternalIntegrationEventMapper>());
        services.AddScoped<PartnerSyncOrchestrator>();
        return services;
    }
}

/// <summary>Partner self-service command/query: ExternalJobSite actor type (handover 6.1, the platform's public partner surface).</summary>
public abstract record PartnerCommand<TResponse> : ICommand<TResponse>, IAuthorizedRequest
{
    public IReadOnlyCollection<ActorType> AllowedActorTypes => new[] { ActorType.ExternalJobSite };

    public string ForbiddenErrorCode => ErrorCodes.PartnerForbidden;
}

public abstract record PartnerQuery<TResponse> : IQuery<TResponse>, IAuthorizedRequest
{
    public IReadOnlyCollection<ActorType> AllowedActorTypes => new[] { ActorType.ExternalJobSite };

    public string ForbiddenErrorCode => ErrorCodes.PartnerForbidden;
}

/// <summary>Administrator command/query (MoL/PEF authority for admission, platform operators for the API framework).</summary>
public abstract record AdminCommand<TResponse> : ICommand<TResponse>, IAuthorizedRequest
{
    public IReadOnlyCollection<ActorType> AllowedActorTypes => new[] { ActorType.Administrator };

    public bool RequireMfa => true;

    public string ForbiddenErrorCode => ErrorCodes.AdminOnly;
}

public abstract record AdminQuery<TResponse> : IQuery<TResponse>, IAuthorizedRequest
{
    public IReadOnlyCollection<ActorType> AllowedActorTypes => new[] { ActorType.Administrator };

    public bool RequireMfa => true;

    public string ForbiddenErrorCode => ErrorCodes.AdminOnly;
}

/// <summary>Service-to-service query (/internal/v1): client-credentials token (actor type System).</summary>
public abstract record ServiceQuery<TResponse> : IQuery<TResponse>, IAuthorizedRequest
{
    public IReadOnlyCollection<ActorType> AllowedActorTypes => new[] { ActorType.System };
}

/// <summary>Anonymous query: handover 6.1 "GET /docs/{version} | anonymous" (3.4.3-03 AC-03).</summary>
public abstract record PublicQuery<TResponse> : IQuery<TResponse>;

public static class ActorFactory
{
    public static Actor From(ICurrentUser user) => new(user.UserId ?? Guid.Empty, user.ActorType == ActorType.Administrator);
}
