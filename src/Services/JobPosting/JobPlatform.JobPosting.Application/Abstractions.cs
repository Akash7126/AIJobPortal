using System.Reflection;
using JobPlatform.JobPosting.Domain;
using JobPlatform.SharedKernel.Application.Interfaces.Cqrs;
using JobPlatform.SharedKernel.Application.Interfaces.Ports;
using JobPlatform.SharedKernel.Common.Enums;
using JobPlatform.SharedKernel.Domain;
using JobPlatform.SharedKernel.Messaging.Interfaces;
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

public sealed record MatchRankingItemView(Guid JobPostingId, string TitleEn, decimal Score);

public sealed record TaxonomySnapshot(string Type, int Version, IReadOnlyCollection<string> ValidCodes);

/// <summary>Q-02 (proposed, decided here): posting is fail-open — an unreachable BC-05 does not block publishing, since BC-05 is not yet built
/// and this BC must remain usable standalone. Only an explicit "not approved" answer blocks. See the BC-09 status doc.</summary>
public static class EmployerEligibilityPolicy
{
    public static bool MayPost(bool? approved) => approved != false;
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
