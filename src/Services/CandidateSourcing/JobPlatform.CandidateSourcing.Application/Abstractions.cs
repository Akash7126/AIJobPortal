using System.Reflection;
using JobPlatform.CandidateSourcing.Domain.Common;
using JobPlatform.SharedKernel.Application.Interfaces.Cqrs;
using JobPlatform.SharedKernel.Application.Interfaces.Ports;
using JobPlatform.SharedKernel.Common.Enums;
using JobPlatform.SharedKernel.Messaging.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace JobPlatform.CandidateSourcing.Application;

/// <summary>Anchor for assembly scanning (handlers, validators) from the composition root.</summary>
public static class ApplicationAssembly
{
    public static Assembly Assembly => typeof(ApplicationAssembly).Assembly;

    public static IServiceCollection AddCandidateSourcingApplication(this IServiceCollection services)
    {
        services.AddSingleton<Events.CandidateSourcingEventMapper>();
        services.AddSingleton<IDomainEventMapper>(sp => sp.GetRequiredService<Events.CandidateSourcingEventMapper>());
        services.AddScoped<Services.Recommendations.CandidateQualificationService>();
        return services;
    }
}

/// <summary>Employer-facing command: Employer actor type, refused with E-CRFE-FORBIDDEN.</summary>
public abstract record EmployerCommand<TResponse> : ICommand<TResponse>, IAuthorizedRequest
{
    public IReadOnlyCollection<ActorType> AllowedActorTypes => new[] { ActorType.Employer };

    public string ForbiddenErrorCode => ErrorCodes.Forbidden;
}

/// <summary>Employer-facing query: Employer actor type, refused with E-CRFE-FORBIDDEN.</summary>
public abstract record EmployerQuery<TResponse> : IQuery<TResponse>, IAuthorizedRequest
{
    public IReadOnlyCollection<ActorType> AllowedActorTypes => new[] { ActorType.Employer };

    public string ForbiddenErrorCode => ErrorCodes.Forbidden;
}

public static class ActorFactory
{
    public static Actor From(ICurrentUser user) => new(user.UserId ?? Guid.Empty);
}

// ---------------------------------------------------------------------- read models

public sealed record FitCriterionView(string Criterion, decimal Score, bool IsStrength, bool IsGap);

public static class CacheKeys
{
    public static readonly TimeSpan RecommendationsTtl = TimeSpan.FromSeconds(60);

    /// <summary>Evicted by MarkPostingMatchesStaleHandler on MatchScoreComputed (handover section 5.2) so the next read re-fetches from BC-10.</summary>
    public static string Recommendations(Guid jobPostingId) => $"recs:{jobPostingId}";
}
