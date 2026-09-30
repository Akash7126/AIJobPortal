using JobPlatform.AiMatching.Application.Commands.Matching;
using JobPlatform.AiMatching.Application.DTOs.Matching;
using JobPlatform.AiMatching.Application.Interfaces;
using JobPlatform.AiMatching.Application.Services.Matching;
using JobPlatform.AiMatching.Domain;
using JobPlatform.SharedKernel.ApiContracts.AiMatching;
using JobPlatform.SharedKernel.Application.Interfaces.Cqrs;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.AiMatching.Application.Handlers.Matching;

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
