using JobPlatform.AiMatching.Application.DTOs.Matching;
using JobPlatform.AiMatching.Application.Queries.Matching;
using JobPlatform.AiMatching.Application.Services.Matching;
using JobPlatform.AiMatching.Domain;
using JobPlatform.SharedKernel.ApiContracts.AiMatching;
using JobPlatform.SharedKernel.Application.Abstractions;
using JobPlatform.SharedKernel.Application.Ports;
using JobPlatform.SharedKernel.Application.Results;
using JobPlatform.SharedKernel.Domain;

namespace JobPlatform.AiMatching.Application.Handlers.Matching;

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
