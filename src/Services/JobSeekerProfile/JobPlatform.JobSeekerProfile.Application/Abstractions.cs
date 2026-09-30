using System.Reflection;
using JobPlatform.SharedKernel.Application.Interfaces.Cqrs;
using JobPlatform.SharedKernel.Common.Enums;
using JobPlatform.SharedKernel.Messaging.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace JobPlatform.JobSeekerProfile.Application;

/// <summary>Anchor for assembly scanning (handlers, validators) from the composition root.</summary>
public static class ApplicationAssembly
{
    public static Assembly Assembly => typeof(ApplicationAssembly).Assembly;

    public static IServiceCollection AddJobSeekerProfileApplication(this IServiceCollection services)
    {
        services.AddSingleton<Events.JobSeekerProfileEventMapper>();
        services.AddSingleton<IDomainEventMapper>(sp => sp.GetRequiredService<Events.JobSeekerProfileEventMapper>());
        return services;
    }
}

/// <summary>Job-seeker-owner command: JobSeeker actor type only, refused with E-JSRPM-FORBIDDEN.</summary>
public abstract record JobSeekerCommand<TResponse> : ICommand<TResponse>, IAuthorizedRequest
{
    public IReadOnlyCollection<ActorType> AllowedActorTypes => new[] { ActorType.JobSeeker };
    public string ForbiddenErrorCode => Domain.Common.ErrorCodes.Forbidden;
}

public abstract record JobSeekerQuery<TResponse> : IQuery<TResponse>, IAuthorizedRequest
{
    public IReadOnlyCollection<ActorType> AllowedActorTypes => new[] { ActorType.JobSeeker };
    public string ForbiddenErrorCode => Domain.Common.ErrorCodes.Forbidden;
}

/// <summary>Employer-owner command (US-3.1.2-08 company documents): Employer actor type, refused with E-ERPM-FORBIDDEN.</summary>
public abstract record EmployerCommand<TResponse> : ICommand<TResponse>, IAuthorizedRequest
{
    public IReadOnlyCollection<ActorType> AllowedActorTypes => new[] { ActorType.Employer };
    public string ForbiddenErrorCode => Domain.Common.ErrorCodes.CompanyForbidden;
}

/// <summary>Service-to-service query (/internal/v1): client-credentials token (actor type System).</summary>
public abstract record ServiceQuery<TResponse> : IQuery<TResponse>, IAuthorizedRequest
{
    public IReadOnlyCollection<ActorType> AllowedActorTypes => new[] { ActorType.System };
}

public static class CacheKeys
{
    public static readonly TimeSpan MatchingViewTtl = TimeSpan.FromMinutes(5);
    public static readonly TimeSpan PrivacyTtl = TimeSpan.FromMinutes(5);
    public static readonly TimeSpan SharedProfileTtl = TimeSpan.FromMinutes(2);

    public static string Matching(Guid profileId) => $"matching:{profileId}";
    public static string Privacy(Guid profileId) => $"privacy:{profileId}";
    public static string Shared(string token) => $"share:{token}";
    public static string Reference(string type) => $"reference:{type}";
}
