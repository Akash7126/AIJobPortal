using FluentValidation;
using JobPlatform.AiMatching.Domain;
using JobPlatform.SharedKernel.ApiContracts.AiMatching;
using JobPlatform.SharedKernel.Application.Abstractions;
using JobPlatform.SharedKernel.Application.Paging;
using JobPlatform.SharedKernel.Application.Ports;
using JobPlatform.SharedKernel.Application.Results;
using JobPlatform.SharedKernel.Domain;

namespace JobPlatform.AiMatching.Application;

// ---------------------------------------------------------------------- requests (handover 6.1)

/// <summary>US-3.3.1-05/06: the caller's ranked jobs (profile from the token), below-threshold matches excluded.</summary>
public sealed record GetJobMatchRankingQuery(decimal? MinScore = null, int Page = 1, int PageSize = 20) : SeekerRequest, IQuery<PagedResult<MatchedJobDto>>;

/// <summary>Internal (BC-09 search): the ranking of an explicit profile.</summary>
public sealed record GetJobMatchRankingForProfileQuery(Guid ProfileId, int Page = 1, int PageSize = 20) : ServiceRequest, IQuery<MatchRankingDto>;

/// <summary>US-3.3.1-01: score with the six-criterion breakdown for one posting; computed on demand when it was below the storage margin.</summary>
public sealed record GetMatchScoreQuery(Guid JobPostingId) : SeekerRequest, IQuery<MatchScoreDetailDto>;

/// <summary>US-3.3.1-06: the employer's reverse-matched candidates of an owned posting (empty is not an error).</summary>
public sealed record GetReverseMatchesQuery(Guid JobPostingId, int Page = 1, int PageSize = 20) : EmployerRequest, IQuery<PagedResult<MatchedCandidateDto>>;

/// <summary>US-3.3.2-02: suitable seekers for an owned, active posting (owner-only, E-JRE-FORBIDDEN). BC-11 owns the employer-facing ranking (Q-05).</summary>
public sealed record GetCandidateRecommendationsQuery(Guid JobPostingId, int Page = 1, int PageSize = 20)
    : EmployerRequest(AiErrorCodes.CandidateRecommendationForbidden), IQuery<PagedResult<MatchedCandidateDto>>;

/// <summary>Internal (BC-11): stored scores of a posting with breakdown.</summary>
public sealed record ListMatchScoresQuery(Guid JobPostingId, decimal? MinScore = null, int Page = 1, int PageSize = 50) : ServiceRequest, IQuery<MatchScoreListDto>;

/// <summary>Internal (BC-04): the parse result of one resume.</summary>
public sealed record GetResumeParsedDataQuery(Guid ResumeParsedDataId) : ServiceRequest, IQuery<ResumeParsedDataDto>;

/// <summary>Persists the score of a pair (worker / operations); the request pipeline wraps it in a transaction and publishes MatchScoreComputed.</summary>
public sealed record ComputeMatchScoreCommand(Guid ProfileId, Guid JobPostingId) : ICommand<MatchScoreDetailDto>;

public sealed record ComputeMatchesForPostingCommand(Guid JobPostingId) : ICommand<int>;

public sealed record ComputeMatchesForProfileCommand(Guid ProfileId) : ICommand<int>;

// ---------------------------------------------------------------------- validators

public sealed class GetJobMatchRankingValidator : AbstractValidator<GetJobMatchRankingQuery>
{
    public GetJobMatchRankingValidator()
    {
        RuleFor(x => x.Page).ValidPage();
        RuleFor(x => x.PageSize).ValidPageSize();
        When(x => x.MinScore.HasValue, () => RuleFor(x => x.MinScore!.Value).InclusiveBetween(0m, 100m).OverridePropertyName("MinScore")
            .WithErrorCode("VAL.MinScore.OutOfRange"));
    }
}

public sealed class GetJobMatchRankingForProfileValidator : AbstractValidator<GetJobMatchRankingForProfileQuery>
{
    public GetJobMatchRankingForProfileValidator()
    {
        RuleFor(x => x.ProfileId).NotEmpty().WithErrorCode("VAL.ProfileId.Required");
        RuleFor(x => x.Page).ValidPage();
        RuleFor(x => x.PageSize).ValidPageSize();
    }
}

public sealed class GetMatchScoreValidator : AbstractValidator<GetMatchScoreQuery>
{
    public GetMatchScoreValidator() => RuleFor(x => x.JobPostingId).NotEmpty().WithErrorCode("VAL.JobPostingId.Required");
}

public sealed class GetReverseMatchesValidator : AbstractValidator<GetReverseMatchesQuery>
{
    public GetReverseMatchesValidator()
    {
        RuleFor(x => x.JobPostingId).NotEmpty().WithErrorCode("VAL.JobPostingId.Required");
        RuleFor(x => x.Page).ValidPage();
        RuleFor(x => x.PageSize).ValidPageSize();
    }
}

public sealed class GetCandidateRecommendationsValidator : AbstractValidator<GetCandidateRecommendationsQuery>
{
    public GetCandidateRecommendationsValidator()
    {
        RuleFor(x => x.JobPostingId).NotEmpty().WithErrorCode("VAL.JobPostingId.Required");
        RuleFor(x => x.Page).ValidPage();
        RuleFor(x => x.PageSize).ValidPageSize();
    }
}

public sealed class ListMatchScoresValidator : AbstractValidator<ListMatchScoresQuery>
{
    public ListMatchScoresValidator()
    {
        RuleFor(x => x.JobPostingId).NotEmpty().WithErrorCode("VAL.JobPostingId.Required");
        RuleFor(x => x.Page).ValidPage();
        RuleFor(x => x.PageSize).ValidPageSize(PageRequest.MaxPageSize);
        When(x => x.MinScore.HasValue, () => RuleFor(x => x.MinScore!.Value).InclusiveBetween(0m, 100m).OverridePropertyName("MinScore")
            .WithErrorCode("VAL.MinScore.OutOfRange"));
    }
}

// ---------------------------------------------------------------------- handlers

internal sealed class GetJobMatchRankingHandler(IKnownProfileRepository profiles, IMatchReadStore store, IMatchingConfigurationProvider configuration, ICurrentUser user)
    : IQueryHandler<GetJobMatchRankingQuery, PagedResult<MatchedJobDto>>
{
    public async Task<Result<PagedResult<MatchedJobDto>>> Handle(GetJobMatchRankingQuery request, CancellationToken ct)
    {
        var page = new PageRequest(request.Page, request.PageSize);
        var profile = user.UserId is { } account ? await profiles.GetByOwnerAsync(account, ct) : null;
        if (profile is null)
        {
            return PageMapping.Of(Array.Empty<MatchedJobDto>(), page, 0); // no profile yet: nothing qualifies, which is a valid (empty) result
        }

        var threshold = (await configuration.GetAsync(ct)).MatchThresholdPercent;
        return await store.ListJobRankingAsync(profile.Id, Math.Max(threshold, request.MinScore ?? 0m), page, ct);
    }
}

internal sealed class GetJobMatchRankingForProfileHandler(IMatchReadStore store, IMatchingConfigurationProvider configuration)
    : IQueryHandler<GetJobMatchRankingForProfileQuery, MatchRankingDto>
{
    public async Task<Result<MatchRankingDto>> Handle(GetJobMatchRankingForProfileQuery request, CancellationToken ct)
    {
        var threshold = (await configuration.GetAsync(ct)).MatchThresholdPercent;
        var page = new PageRequest(request.Page, request.PageSize);
        var result = await store.ListJobRankingAsync(request.ProfileId, threshold, page, ct);
        return new MatchRankingDto(result.Items.Select(i => new MatchRankingItemDto(i.JobPostingId, i.Title, i.Score, i.ComputedAtUtc)).ToArray(), result.Page,
            result.PageSize, result.TotalCount, threshold);
    }
}

internal sealed class GetMatchScoreHandler(IKnownProfileRepository knownProfiles, IMatchScoreRepository scores, IKnownPostingRepository knownPostings,
    MatchComputationService computation, IProfileDirectory profiles, IMatchingConfigurationProvider configuration, ICurrentUser user, TimeProvider clock)
    : IQueryHandler<GetMatchScoreQuery, MatchScoreDetailDto>
{
    public async Task<Result<MatchScoreDetailDto>> Handle(GetMatchScoreQuery request, CancellationToken ct)
    {
        var known = user.UserId is { } account ? await knownProfiles.GetByOwnerAsync(account, ct) : null;
        if (known is null)
        {
            return Error.NotFound(AiErrorCodes.ProfileUnknown, "No profile is known for the caller yet.");
        }

        var config = await configuration.GetAsync(ct);
        var posting = await knownPostings.GetAsync(request.JobPostingId, ct);
        var stored = await scores.GetPairAsync(known.Id, request.JobPostingId, ct);
        if (stored is not null && posting is { IsActive: true })
        {
            return ToDetail(stored.Id, request.JobPostingId, posting.Title, stored.Score, stored.ConfigVersion, true, stored.ComputedAtUtc, stored.Breakdown, config);
        }

        // Below the storage margin (or not yet computed): compute on demand, without persisting (queries never change state).
        try
        {
            var loaded = await computation.LoadPostingAsync(request.JobPostingId, refreshSemantics: false, ct);
            var profile = await profiles.GetMatchViewAsync(known.Id, ct);
            if (loaded is not { View.IsActive: true } || profile is null)
            {
                return Error.NotFound(AiErrorCodes.PostingNotFound, "The posting was not found or is not active.");
            }

            var similarity = await computation.SimilarityAsync(profile, loaded.Value.View, ct);
            var result = MatchScore.Evaluate(profile, loaded.Value.View, config, similarity);
            return ToDetail(null, request.JobPostingId, loaded.Value.Source.Title, result.Score, config.ConfigVersion, false, clock.GetUtcNow().UtcDateTime, result.Breakdown, config);
        }
        catch (UpstreamUnavailableException ex)
        {
            return Error.External(AiErrorCodes.UpstreamUnavailable, ex.Message);
        }
        catch (BusinessRuleViolationException ex)
        {
            return ex.ToError();
        }
    }

    private static MatchScoreDetailDto ToDetail(Guid? id, Guid postingId, string? title, decimal score, int configVersion, bool stored, DateTime at,
        IReadOnlyList<CriterionResult> breakdown, MatchingConfigSnapshot config) =>
        new(id, postingId, title, score, score >= config.MatchThresholdPercent, configVersion, stored, at,
            breakdown.Select(b => new MatchCriterionDto(b.Criterion.ToString(), b.Score, b.Weight, b.Included)).ToArray());
}

/// <summary>Owner-only view of an owned posting; unknown postings are 404, foreign postings refused with the story's own code.</summary>
internal static class PostingOwnership
{
    public static async Task<Result<KnownPosting>> RequireOwnedAsync(IKnownPostingRepository postings, ICurrentUser user, Guid postingId, string forbiddenCode,
        CancellationToken ct)
    {
        var posting = await postings.GetAsync(postingId, ct);
        if (posting is null)
        {
            return Error.NotFound(AiErrorCodes.PostingNotFound, "The posting was not found.");
        }

        return posting.EmployerAccountId == user.UserId ? posting : Error.Forbidden(forbiddenCode, "Only the owner of the posting may see its candidates.");
    }
}

internal sealed class GetReverseMatchesHandler(IKnownPostingRepository postings, IMatchReadStore store, IMatchingConfigurationProvider configuration, ICurrentUser user)
    : IQueryHandler<GetReverseMatchesQuery, PagedResult<MatchedCandidateDto>>
{
    public async Task<Result<PagedResult<MatchedCandidateDto>>> Handle(GetReverseMatchesQuery request, CancellationToken ct)
    {
        var owned = await PostingOwnership.RequireOwnedAsync(postings, user, request.JobPostingId, AiErrorCodes.Forbidden, ct);
        if (owned.IsFailure)
        {
            return owned.Error!;
        }

        var threshold = (await configuration.GetAsync(ct)).MatchThresholdPercent;
        return await store.ListCandidatesAsync(request.JobPostingId, threshold, new PageRequest(request.Page, request.PageSize), ct);
    }
}

internal sealed class GetCandidateRecommendationsHandler(IKnownPostingRepository postings, IMatchReadStore store, IMatchingConfigurationProvider configuration,
    ICurrentUser user) : IQueryHandler<GetCandidateRecommendationsQuery, PagedResult<MatchedCandidateDto>>
{
    public async Task<Result<PagedResult<MatchedCandidateDto>>> Handle(GetCandidateRecommendationsQuery request, CancellationToken ct)
    {
        var owned = await PostingOwnership.RequireOwnedAsync(postings, user, request.JobPostingId, AiErrorCodes.CandidateRecommendationForbidden, ct);
        if (owned.IsFailure)
        {
            return owned.Error!;
        }

        var page = new PageRequest(request.Page, request.PageSize);
        if (!owned.Value.IsActive)
        {
            return PageMapping.Of(Array.Empty<MatchedCandidateDto>(), page, 0); // suitable seekers exist only for active postings
        }

        var threshold = (await configuration.GetAsync(ct)).MatchThresholdPercent;
        return await store.ListCandidatesAsync(request.JobPostingId, threshold, page, ct);
    }
}

internal sealed class ListMatchScoresHandler(IMatchReadStore store, IMatchingConfigurationProvider configuration)
    : IQueryHandler<ListMatchScoresQuery, MatchScoreListDto>
{
    public async Task<Result<MatchScoreListDto>> Handle(ListMatchScoresQuery request, CancellationToken ct)
    {
        var config = await configuration.GetAsync(ct);
        var page = await store.ListScoresWithBreakdownAsync(request.JobPostingId, request.MinScore ?? 0m, new PageRequest(request.Page, request.PageSize), ct);
        return new MatchScoreListDto(page.Items, page.Page, page.PageSize, page.TotalCount, config.MatchThresholdPercent, config.ConfigVersion.ToString());
    }
}

internal sealed class GetResumeParsedDataHandler(IMatchReadStore store) : IQueryHandler<GetResumeParsedDataQuery, ResumeParsedDataDto>
{
    public async Task<Result<ResumeParsedDataDto>> Handle(GetResumeParsedDataQuery request, CancellationToken ct) =>
        await store.GetResumeParsedDataAsync(request.ResumeParsedDataId, ct) is { } dto ? dto : Error.NotFound(AiErrorCodes.NotFound, "The parsed data was not found.");
}

// ---------------------------------------------------------------------- commands (worker / operations)

internal sealed class ComputeMatchScoreHandler(MatchComputationService computation, IProfileDirectory profiles, IMatchingConfigurationProvider configuration)
    : ICommandHandler<ComputeMatchScoreCommand, MatchScoreDetailDto>
{
    public async Task<Result<MatchScoreDetailDto>> Handle(ComputeMatchScoreCommand request, CancellationToken ct)
    {
        try
        {
            var config = await configuration.GetAsync(ct);
            var loaded = await computation.LoadPostingAsync(request.JobPostingId, refreshSemantics: false, ct);
            var profile = await profiles.GetMatchViewAsync(request.ProfileId, ct);
            if (loaded is null || profile is null)
            {
                return Error.NotFound(AiErrorCodes.NotFound, "The profile or the posting was not found.");
            }

            var outcome = await computation.ScoreAsync(profile, loaded.Value.View, config, ct);
            var breakdown = outcome.Stored?.Breakdown ?? Array.Empty<CriterionResult>();
            return new MatchScoreDetailDto(outcome.Stored?.Id, request.JobPostingId, loaded.Value.Source.Title, outcome.Score, outcome.Score >= config.MatchThresholdPercent,
                config.ConfigVersion, outcome.Stored is not null, DateTime.UtcNow,
                breakdown.Select(b => new MatchCriterionDto(b.Criterion.ToString(), b.Score, b.Weight, b.Included)).ToArray());
        }
        catch (UpstreamUnavailableException ex)
        {
            return Error.External(AiErrorCodes.UpstreamUnavailable, ex.Message);
        }
    }
}

internal sealed class ComputeMatchesForPostingHandler(MatchComputationService computation) : ICommandHandler<ComputeMatchesForPostingCommand, int>
{
    public async Task<Result<int>> Handle(ComputeMatchesForPostingCommand request, CancellationToken ct) => await computation.ComputeForPostingAsync(request.JobPostingId, ct);
}

internal sealed class ComputeMatchesForProfileHandler(MatchComputationService computation) : ICommandHandler<ComputeMatchesForProfileCommand, int>
{
    public async Task<Result<int>> Handle(ComputeMatchesForProfileCommand request, CancellationToken ct) => await computation.ComputeForProfileAsync(request.ProfileId, ct);
}
