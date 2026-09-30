using System.Reflection;
using JobPlatform.EmployerOnboarding.Domain.Common;
using JobPlatform.SharedKernel.Application.Interfaces.Cqrs;
using JobPlatform.SharedKernel.Common.Enums;
using Microsoft.Extensions.DependencyInjection;

namespace JobPlatform.EmployerOnboarding.Application;

/// <summary>Anchor for assembly scanning (handlers, validators) from the composition root.</summary>
public static class ApplicationAssembly
{
    public static Assembly Assembly => typeof(ApplicationAssembly).Assembly;

    public static IServiceCollection AddEmployerOnboardingApplication(this IServiceCollection services)
    {
        services.AddSingleton<Events.EmployerOnboardingEventMapper>();
        services.AddSingleton<SharedKernel.Messaging.Interfaces.IDomainEventMapper>(sp => sp.GetRequiredService<Events.EmployerOnboardingEventMapper>());
        return services;
    }
}

/// <summary>Employer self-service command/query: Employer actor type, no MFA requirement.</summary>
public abstract record EmployerCommand<TResponse> : ICommand<TResponse>, IAuthorizedRequest
{
    public IReadOnlyCollection<ActorType> AllowedActorTypes => new[] { ActorType.Employer };

    public string ForbiddenErrorCode => Domain.Common.ErrorCodes.MediaForbidden;
}

public abstract record EmployerQuery<TResponse> : IQuery<TResponse>, IAuthorizedRequest
{
    public IReadOnlyCollection<ActorType> AllowedActorTypes => new[] { ActorType.Employer };

    public string ForbiddenErrorCode => Domain.Common.ErrorCodes.MediaForbidden;
}

/// <summary>Administrator command/query: Administrator actor type with MFA, refused with E-AUM-FORBIDDEN.</summary>
public abstract record AdminCommand<TResponse> : ICommand<TResponse>, IAuthorizedRequest
{
    public IReadOnlyCollection<ActorType> AllowedActorTypes => new[] { ActorType.Administrator };

    public bool RequireMfa => true;

    public string ForbiddenErrorCode => Domain.Common.ErrorCodes.RegistrationForbidden;
}

public abstract record AdminQuery<TResponse> : IQuery<TResponse>, IAuthorizedRequest
{
    public IReadOnlyCollection<ActorType> AllowedActorTypes => new[] { ActorType.Administrator };

    public bool RequireMfa => true;

    public string ForbiddenErrorCode => Domain.Common.ErrorCodes.RegistrationForbidden;
}

/// <summary>Service-to-service query (/internal/v1): client-credentials token (actor type System).</summary>
public abstract record ServiceQuery<TResponse> : IQuery<TResponse>, IAuthorizedRequest
{
    public IReadOnlyCollection<ActorType> AllowedActorTypes => new[] { ActorType.System };
}

public static class ActorFactory
{
    public static Actor From(SharedKernel.Application.Interfaces.Ports.ICurrentUser user) => new(user.UserId ?? Guid.Empty, user.ActorType == ActorType.Administrator);
}

public sealed record ScanResult(bool Clean, string? Reason);
