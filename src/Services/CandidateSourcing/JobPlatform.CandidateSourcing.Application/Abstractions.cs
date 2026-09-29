using System.Reflection;
using JobPlatform.CandidateSourcing.Application.DTOs.Search;
using JobPlatform.CandidateSourcing.Application.DTOs.TalentPool;
using JobPlatform.CandidateSourcing.Domain.Common;
using JobPlatform.SharedKernel.Application.Abstractions;
using JobPlatform.SharedKernel.Application.Paging;
using JobPlatform.SharedKernel.Application.Ports;
using JobPlatform.SharedKernel.Common.Enums;
using JobPlatform.SharedKernel.Messaging;
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

// ---------------------------------------------------------------------- ports

/// <summary>Read side (foundation section 3.5): the candidate database search and the talent pool listing, over the local projection and entries.</summary>
public interface ICandidateSourcingReadStore
{
    Task<IReadOnlyList<TalentPoolEntryView>> ListTalentPoolAsync(Guid employerAccountId, CancellationToken ct = default);

    Task<PagedResult<CandidateSearchResultItemView>> SearchCandidatesAsync(CandidateSearchCriteria criteria, PageRequest page, CancellationToken ct = default);
}

/// <summary>Cache-aside store for the derived data this BC recomputes often (foundation section 10). Failures degrade to a fresh computation, never an error.</summary>
public interface ICandidateSourcingCache
{
    Task<T?> GetAsync<T>(string key, CancellationToken ct = default);

    Task SetAsync<T>(string key, T value, TimeSpan ttl, CancellationToken ct = default);

    Task RemoveAsync(string key, CancellationToken ct = default);
}

public static class CacheKeys
{
    public static readonly TimeSpan RecommendationsTtl = TimeSpan.FromSeconds(60);

    /// <summary>Evicted by MarkPostingMatchesStaleHandler on MatchScoreComputed (handover section 5.2) so the next read re-fetches from BC-10.</summary>
    public static string Recommendations(Guid jobPostingId) => $"recs:{jobPostingId}";
}
