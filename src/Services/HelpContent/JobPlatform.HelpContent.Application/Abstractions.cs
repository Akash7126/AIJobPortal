using System.Reflection;
using JobPlatform.HelpContent.Application.DTOs.Common;
using JobPlatform.HelpContent.Domain.Common;
using JobPlatform.SharedKernel.Application.Interfaces.Cqrs;
using JobPlatform.SharedKernel.Common.Enums;
using Microsoft.Extensions.DependencyInjection;

namespace JobPlatform.HelpContent.Application;

/// <summary>Anchor for assembly scanning (handlers, validators) from the composition root.</summary>
public static class ApplicationAssembly
{
    public static Assembly Assembly => typeof(ApplicationAssembly).Assembly;

    public static IServiceCollection AddHelpContentApplication(this IServiceCollection services)
    {
        services.AddSingleton<Events.HelpContentEventMapper>();
        services.AddSingleton<SharedKernel.Messaging.Interfaces.IDomainEventMapper>(sp => sp.GetRequiredService<Events.HelpContentEventMapper>());
        return services;
    }
}

/// <summary>Administrator command/query: authoring surface of both News and Help/FAQ (handover section 6.1, ADMIN_ONLY family).</summary>
public abstract record AdminCommand<TResponse> : ICommand<TResponse>, IAuthorizedRequest
{
    public IReadOnlyCollection<ActorType> AllowedActorTypes => new[] { ActorType.Administrator };

    public bool RequireMfa => true;

    /// <summary>Overridden per-request where the handover distinguishes E-NEWSU-FORBIDDEN from E-FAQHC-FORBIDDEN.</summary>
    public virtual string ForbiddenErrorCode => ErrorCodes.NewsForbidden;
}

public abstract record AdminQuery<TResponse> : IQuery<TResponse>, IAuthorizedRequest
{
    public IReadOnlyCollection<ActorType> AllowedActorTypes => new[] { ActorType.Administrator };

    public bool RequireMfa => true;

    public virtual string ForbiddenErrorCode => ErrorCodes.NewsForbidden;
}

/// <summary>Any authenticated caller (job seeker, employer, external site or administrator) - e.g. feedback, tutorials, company-page edit.</summary>
public abstract record AuthenticatedCommand<TResponse> : ICommand<TResponse>, IAuthorizedRequest;

public abstract record AuthenticatedQuery<TResponse> : IQuery<TResponse>, IAuthorizedRequest;

public static class ActorFactory
{
    public static Actor From(SharedKernel.Application.Interfaces.Ports.ICurrentUser user) => new(user.UserId ?? Guid.Empty, user.ActorType == ActorType.Administrator);
}

public sealed record HelpContentVersionView(int VersionNo, LocalizedView Title, LocalizedView Body, Guid EditedBy, DateTime EditedAtUtc);

public sealed record CompanyDirectoryEntry(string Name, string? LogoUrl, string Industry, string CompanySize, string Website, bool Verified, string? Badge);
