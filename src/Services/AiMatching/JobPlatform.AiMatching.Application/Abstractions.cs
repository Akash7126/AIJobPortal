using System.Reflection;
using JobPlatform.AiMatching.Application.Services.Matching;
using JobPlatform.AiMatching.Application.Services.Parsing;
using JobPlatform.AiMatching.Application.Services.Semantics;
using JobPlatform.AiMatching.Domain;
using JobPlatform.SharedKernel.Application.Interfaces.Cqrs;
using JobPlatform.SharedKernel.Application.Paging;
using JobPlatform.SharedKernel.Common.Enums;
using Microsoft.Extensions.DependencyInjection;

namespace JobPlatform.AiMatching.Application;

/// <summary>Anchor for assembly scanning (handlers, validators) from the composition root.</summary>
public static class ApplicationAssembly
{
    public static Assembly Assembly => typeof(ApplicationAssembly).Assembly;

    public static IServiceCollection AddAiMatchingApplication(this IServiceCollection services)
    {
        services.AddScoped<MatchComputationService>();
        services.AddScoped<MatchingWorkRunner>();
        services.AddScoped<ResumeParsingService>();
        services.AddScoped<PostingAnalysisService>();
        services.AddScoped<PostingStatusReaction>();
        services.AddSingleton<AiMatchingEventMapper>();
        services.AddSingleton<JobPlatform.SharedKernel.Messaging.Interfaces.IDomainEventMapper>(sp => sp.GetRequiredService<AiMatchingEventMapper>());
        return services;
    }
}

/// <summary>Tunables of the matching engine (section "Matching").</summary>
public sealed class MatchingOptions
{
    public const string SectionName = "Matching";

    /// <summary>D-01: scores at or above (threshold - margin) are persisted; lower ones are computed on demand.</summary>
    public decimal StorageMarginPercent { get; set; } = 10m;

    /// <summary>How many nearest neighbours the vector index returns as candidates for a posting or profile.</summary>
    public int CandidateRetrievalLimit { get; set; } = 500;

    /// <summary>Embedding cosine at or above which two different skill terms count as near-synonyms.</summary>
    public double SynonymSimilarityThreshold { get; set; } = 0.75;

    public int MaxShortlistSize { get; set; } = 1000;
    public int MaxRecommendations { get; set; } = 50;
    public int WorkBatchSize { get; set; } = 10;
    public int WorkMaxAttempts { get; set; } = 5;
    public TimeSpan WorkPollInterval { get; set; } = TimeSpan.FromSeconds(2);
    public bool WorkerEnabled { get; set; } = true;

    /// <summary>Weekly recommendation scheduler (BC-13 delivers the weekly mail from JobRecommendationComputed).</summary>
    public bool WeeklyRecommendationsEnabled { get; set; } = true;
    public TimeSpan WeeklyRecommendationsInterval { get; set; } = TimeSpan.FromDays(7);
}

// ---------------------------------------------------------------------- request bases (role, forbidden code)

/// <summary>Base for requests only a given actor type may make; the refusal carries the story's own error code.</summary>
public abstract record ActorRequest(ActorType Actor, string ForbiddenCode) : IAuthorizedRequest
{
    public IReadOnlyCollection<ActorType> AllowedActorTypes => new[] { Actor };

    public string ForbiddenErrorCode => ForbiddenCode;
}

/// <summary>Administrator-only requests: Administrator actor type with MFA (THR-029).</summary>
public abstract record AdminRequest : ActorRequest, IAuthorizedRequest
{
    protected AdminRequest() : base(ActorType.Administrator, AiMatchingCodes.Forbidden)
    {
    }

    bool IAuthorizedRequest.RequireMfa => true;
}

public abstract record SeekerRequest() : ActorRequest(ActorType.JobSeeker, AiMatchingCodes.Forbidden);

public abstract record EmployerRequest(string ForbiddenCode = AiMatchingCodes.Forbidden) : ActorRequest(ActorType.Employer, ForbiddenCode);

/// <summary>Service-to-service (internal API) requests: the token's actor type is System.</summary>
public abstract record ServiceRequest() : ActorRequest(ActorType.System, AiMatchingCodes.Forbidden);

public static class AiMatchingCodes
{
    public const string Forbidden = AiErrorCodes.Forbidden;
}

public static class ActorMapping
{
    public static Actor ToActor(this JobPlatform.SharedKernel.Application.Interfaces.Ports.ICurrentUser user) =>
        new(user.UserId ?? Guid.Empty, user.ActorType ?? ActorType.Guest);
}

/// <summary>The other BC (or the model runtime) is unreachable. Command handlers turn it into ErrorType.External; inbox/worker callers let it propagate so the work is retried.</summary>
public sealed class UpstreamUnavailableException : Exception
{
    public UpstreamUnavailableException(string message, Exception? inner = null) : base(message, inner)
    {
    }
}

/// <summary>Skill embedding/vector entity types.</summary>
public enum VectorEntityType
{
    Profile,
    Posting
}

public sealed record VectorHit(Guid EntityId, double Similarity);

public static class PageMapping
{
    public static PagedResult<T> Of<T>(IReadOnlyList<T> items, PageRequest page, int total) => new(items, page.Page, page.PageSize, total);
}
