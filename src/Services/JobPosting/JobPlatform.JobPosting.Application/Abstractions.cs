using System.Reflection;
using JobPlatform.JobPosting.Domain;
using JobPlatform.SharedKernel.Application.Abstractions;
using JobPlatform.SharedKernel.Application.Paging;
using JobPlatform.SharedKernel.Application.Ports;
using JobPlatform.SharedKernel.Common.Enums;
using JobPlatform.SharedKernel.Domain;
using JobPlatform.SharedKernel.Messaging;
using Microsoft.Extensions.DependencyInjection;

namespace JobPlatform.JobPosting.Application;

/// <summary>Anchor for assembly scanning (handlers, validators) from the composition root.</summary>
public static class ApplicationAssembly
{
    public static Assembly Assembly => typeof(ApplicationAssembly).Assembly;

    public static IServiceCollection AddJobPostingApplication(this IServiceCollection services)
    {
        services.AddSingleton<Events.JobPostingEventMapper>();
        services.AddSingleton<IDomainEventMapper>(sp => sp.GetRequiredService<Events.JobPostingEventMapper>());
        services.AddScoped<Events.SavedSearchMatchEvaluator>();
        return services;
    }
}

/// <summary>Employer-facing command: Employer actor type, refused with E-JCP-FORBIDDEN by convention per handler.</summary>
public abstract record EmployerCommand<TResponse> : ICommand<TResponse>, IAuthorizedRequest
{
    public IReadOnlyCollection<ActorType> AllowedActorTypes => new[] { ActorType.Employer };

    public string ForbiddenErrorCode => ErrorCodes.PostingForbidden;
}

/// <summary>Employer-facing status-change command: refused with E-JST-FORBIDDEN (handover section 3.1 status family).</summary>
public abstract record EmployerStatusCommand<TResponse> : ICommand<TResponse>, IAuthorizedRequest
{
    public IReadOnlyCollection<ActorType> AllowedActorTypes => new[] { ActorType.Employer };

    public string ForbiddenErrorCode => ErrorCodes.StatusForbidden;
}

/// <summary>Job-seeker-facing command (favourites, saved searches): refused with E-JSF-FORBIDDEN by convention per handler.</summary>
public abstract record JobSeekerCommand<TResponse> : ICommand<TResponse>, IAuthorizedRequest
{
    public IReadOnlyCollection<ActorType> AllowedActorTypes => new[] { ActorType.JobSeeker };

    public string ForbiddenErrorCode => ErrorCodes.FavoriteForbidden;
}

/// <summary>Job-seeker-facing command on the interested list: refused with E-JIP-FORBIDDEN by convention per handler.</summary>
public abstract record JobSeekerInterestedCommand<TResponse> : ICommand<TResponse>, IAuthorizedRequest
{
    public IReadOnlyCollection<ActorType> AllowedActorTypes => new[] { ActorType.JobSeeker };

    public string ForbiddenErrorCode => ErrorCodes.InterestedForbidden;
}

/// <summary>Job-seeker-facing query.</summary>
public abstract record JobSeekerQuery<TResponse> : IQuery<TResponse>, IAuthorizedRequest
{
    public IReadOnlyCollection<ActorType> AllowedActorTypes => new[] { ActorType.JobSeeker };
}

/// <summary>Employer-facing query.</summary>
public abstract record EmployerQuery<TResponse> : IQuery<TResponse>, IAuthorizedRequest
{
    public IReadOnlyCollection<ActorType> AllowedActorTypes => new[] { ActorType.Employer };
}

/// <summary>Public/guest query (US-4.1-02): no authentication required (anonymous, per-role filtering applied inside the handler).</summary>
public abstract record PublicQuery<TResponse> : IQuery<TResponse>;

/// <summary>Service-to-service query (/internal/v1): client-credentials token (actor type System).</summary>
public abstract record ServiceQuery<TResponse> : IQuery<TResponse>, IAuthorizedRequest
{
    public IReadOnlyCollection<ActorType> AllowedActorTypes => new[] { ActorType.System };
}

/// <summary>
/// Result of a posting mutation. Deliberately does not carry the aggregate's RowVersion: a SQL Server rowversion (and the SQLite-stamped
/// equivalent) is only assigned when SaveChanges runs, which happens after the handler returns inside the same command pipeline
/// (foundation section 6) - any byte[] captured here would be the pre-save value, not the true new ETag. Callers that need the current
/// ETag re-read it from <see cref="JobPostingView"/> (GetJobPostingQuery), which always reflects the committed state.
/// </summary>
public sealed record PostingMutationResult(Guid JobPostingId, bool Existing = false, bool Overwritten = false);

public static class ActorFactory
{
    public static Actor From(ICurrentUser user) =>
        new(user.UserId ?? Guid.Empty, user.ActorType == ActorType.Administrator, user.ActorType == ActorType.System);
}

/// <summary>Throws when a taxonomy-backed schema validation failed (INV-02), so the unit-of-work behavior maps it like any other domain rule.</summary>
public static class SchemaValidationGuard
{
    public static void EnsureValid(SchemaValidationResult result)
    {
        if (result.IsValid)
        {
            return;
        }

        throw new BusinessRuleViolationException(RuleCodes.PostingSchemaInvalid, string.Join("; ", result.Errors), ErrorCodes.InvalidField,
            BusinessRuleKind.InvalidInput, new Dictionary<string, object?> { ["field"] = "schema", ["violations"] = result.Errors.ToArray() });
    }
}

// ---------------------------------------------------------------------- read models

public sealed record LocalizedView(string Ar, string En);

public sealed record SalaryRangeView(decimal? Min, decimal? Max, string? Currency);

public sealed record JobLocationView(string? Governorate, string? City);

public sealed record JobVisibilityView(string Scope, IReadOnlyList<Guid> TargetJobSeekerIds);

public sealed record JobPostingView(
    Guid JobPostingId, Guid? EmployerAccountId, string Source, LocalizedView Title, LocalizedView Summary, IReadOnlyList<string> Skills,
    string CategoryCode, string ContractType, string? EducationLevel, IReadOnlyList<string> RequiredTraining, string WorkFormat,
    JobLocationView? Location, SalaryRangeView? Salary, int? MinExperienceYears, int? MaxExperienceYears, IReadOnlyList<string> RequiredLanguages,
    DateTime DeadlineUtc, bool AutoClose, string? JobLink, IReadOnlyDictionary<string, string> OtherFields, JobVisibilityView Visibility,
    string Status, bool AdminSuspended, int TaxonomyVersion, DateTime CreatedAtUtc, DateTime? PublishedAtUtc, DateTime UpdatedAtUtc, byte[] RowVersion);

public sealed record JobPostingSummaryView(
    Guid JobPostingId, LocalizedView Title, string CategoryCode, JobLocationView? Location, SalaryRangeView? Salary, DateTime DeadlineUtc, string Status,
    string ContractType, DateTime? PublishedAtUtc, decimal? RelevanceScore);

public sealed record JobPostingSchemaFieldView(string Name, bool Required, string Type, IReadOnlyList<string>? AllowedValues);

public sealed record JobPostingSchemaView(int TaxonomyVersion, IReadOnlyList<JobPostingSchemaFieldView> Fields, IReadOnlyList<string> CategoryCodes,
    IReadOnlyList<string> SkillCodes);

/// <summary>Result of a job-posting search (foundation section 12: THR-013 &lt;= 2s).</summary>
public sealed record SearchCriteriaInput(
    string? Keyword, string? Governorate, string? City, decimal? SalaryMin, decimal? SalaryMax, string? ContractType, DateTime? PostedAfterUtc,
    DateTime? DeadlineBeforeUtc, string? CategoryCode)
{
    public SearchCriteria ToDomain() => new(Keyword, Governorate, City, SalaryMin, SalaryMax,
        ContractType is null ? null : Enum.Parse<Domain.ContractType>(ContractType, true), PostedAfterUtc, DeadlineBeforeUtc, CategoryCode);
}

// ---------------------------------------------------------------------- ports

/// <summary>Read/search side (foundation section 3.5): dedicated projections over EF, never the aggregate. Backed by SQL Server full-text search
/// in production; a LIKE-based fallback on SQLite (dev/tests) — see the Infrastructure implementation and the BC-09 status doc.</summary>
public interface IJobPostingSearchReadModel
{
    Task<PagedResult<JobPostingSummaryView>> SearchAsync(SearchCriteriaInput criteria, string? sort, PageRequest page, CancellationToken ct = default);

    Task<JobPostingView?> GetAsync(Guid id, CancellationToken ct = default);

    Task<PagedResult<JobPostingSummaryView>> ListByEmployerAsync(Guid employerAccountId, string? status, PageRequest page, CancellationToken ct = default);

    Task<PagedResult<JobPostingSummaryView>> ListOpenByEmployerAsync(Guid employerAccountId, PageRequest page, CancellationToken ct = default);

    /// <summary>Which of the given codes of this reference type still appear on a non-archived posting (US-3.1.4-07, /internal/v1/reference-usage/check).</summary>
    Task<IReadOnlyList<string>> CheckReferenceUsageAsync(string type, IReadOnlyCollection<string> codes, CancellationToken ct = default);

    Task<JobPostingSchemaView> GetSchemaAsync(CancellationToken ct = default);
}

public sealed record FavoriteListView(Guid FavoriteJobListId, IReadOnlyList<Guid> JobPostingIds);

public sealed record SavedSearchView(Guid SavedSearchId, SearchCriteriaInput Criteria, bool NotifyOnMatch, DateTime CreatedAtUtc, DateTime? LastEvaluatedAtUtc);

public sealed record InterestedListItemView(Guid InterestedListEntryId, string ReferenceType, Guid? PostingId, SearchCriteriaInput? Criteria, DateTime CreatedAtUtc);

public sealed record MatchRankingItemView(Guid JobPostingId, string TitleEn, decimal Score);

/// <summary>Port to BC-08's taxonomy (skills/jobs/trainings), cached in Redis (handover section 9). Adapter chosen by Taxonomy:Provider.</summary>
public interface ITaxonomyProvider
{
    /// <summary>The current version and valid codes for a taxonomy type ("skills", "jobs" categories, "trainings"). Throws
    /// <see cref="TaxonomyUnavailableException"/> after its retry budget (30 s / 3 retries, handover section 3.5).</summary>
    Task<TaxonomySnapshot> GetAsync(string type, CancellationToken ct = default);
}

public sealed record TaxonomySnapshot(string Type, int Version, IReadOnlyCollection<string> ValidCodes);

/// <summary>Port to BC-05: is this employer approved/verified to post (handover section 6.2, Q-02)? Adapter chosen by EmployerStanding:Provider.</summary>
public interface IEmployerStandingProvider
{
    /// <summary>Null when BC-05 could not be reached; the caller applies the fail-open policy of <see cref="EmployerEligibilityPolicy"/>.</summary>
    Task<bool?> IsApprovedAsync(Guid employerAccountId, CancellationToken ct = default);
}

/// <summary>Q-02 (proposed, decided here): posting is fail-open — an unreachable BC-05 does not block publishing, since BC-05 is not yet built
/// and this BC must remain usable standalone. Only an explicit "not approved" answer blocks. See the BC-09 status doc.</summary>
public static class EmployerEligibilityPolicy
{
    public static bool MayPost(bool? approved) => approved != false;
}

/// <summary>Port to BC-10: recommended jobs for a logged-in job seeker (handover section 6.2). Adapter chosen by MatchRanking:Provider.</summary>
public interface IMatchRankingProvider
{
    /// <summary>Empty when BC-10 is unavailable - the caller degrades to plain search results (handover section 6.2).</summary>
    Task<IReadOnlyList<MatchRankingItemView>> GetRankingAsync(Guid profileId, int page, int pageSize, CancellationToken ct = default);
}

/// <summary>Cache-aside store for reference data (foundation section 10). Failures degrade to the database, never to an error.</summary>
public interface IJobPostingCache
{
    Task<T?> GetAsync<T>(string key, CancellationToken ct = default);

    Task SetAsync<T>(string key, T value, TimeSpan ttl, CancellationToken ct = default);

    Task RemoveAsync(string key, CancellationToken ct = default);

    Task RemoveByPrefixAsync(string prefix, CancellationToken ct = default);
}

/// <summary>Key names and TTLs of handover section 9 (the store adds the "&lt;env&gt;:job-posting" prefix).</summary>
public static class CacheKeys
{
    public static readonly TimeSpan TaxonomyTtl = TimeSpan.FromMinutes(60);
    public static readonly TimeSpan SearchTtl = TimeSpan.FromSeconds(60);
    public static readonly TimeSpan PostingTtl = TimeSpan.FromMinutes(5);
    public static readonly TimeSpan EmployerStandingTtl = TimeSpan.FromMinutes(5);

    public static string Taxonomy(string type) => $"taxonomy:{type}";

    public static string Posting(Guid id) => $"posting:{id}";

    public static string EmployerStanding(Guid id) => $"employer-standing:{id}";

    public const string ExpiryJobLock = "lock:expiry-job";
}
