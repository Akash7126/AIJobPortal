using JobPlatform.AiMatching.Application.Commands.Shortlists;
using JobPlatform.AiMatching.Application.Interfaces;
using JobPlatform.AiMatching.Application.Services.Matching;
using JobPlatform.AiMatching.Domain;
using JobPlatform.AiMatching.Domain.Interfaces.Repositories;
using JobPlatform.SharedKernel.Application.Interfaces.Cqrs;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.AiMatching.Application.Handlers.Shortlists;

internal sealed class RunCandidateShortlistHandler(ICandidateShortlistRepository shortlists, MatchComputationService computation, IMatchScoreRepository scores,
    IKnownProfileRepository profiles, IMatchingConfigurationProvider configuration, TimeProvider clock) : ICommandHandler<RunCandidateShortlistCommand, Unit>
{
    public async Task<Result<Unit>> Handle(RunCandidateShortlistCommand request, CancellationToken ct)
    {
        var shortlist = await shortlists.GetAsync(request.ShortlistId, ct);
        if (shortlist is not { Status: ShortlistStatus.Queued })
        {
            return Result.Success(); // already done (redelivery) or unknown
        }

        var now = clock.GetUtcNow().UtcDateTime;
        var config = await configuration.GetAsync(ct);
        await computation.ComputeForPostingAsync(shortlist.JobPostingId, ct);
        var candidates = new List<(Guid, decimal)>();
        foreach (var score in await scores.ListByPostingAsync(shortlist.JobPostingId, ct))
        {
            if (score.Score >= config.MatchThresholdPercent && await profiles.GetAsync(score.ProfileId, ct) is { Standing: KnownStanding.Active })
            {
                candidates.Add((score.ProfileId, score.Score));
            }
        }

        shortlist.Complete(candidates, now); // fewer than N qualify: all of them, no padding
        return Result.Success();
    }
}
