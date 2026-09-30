using JobPlatform.AiMatching.Application.Commands.Recommendations;
using JobPlatform.AiMatching.Application.DTOs.Recommendations;
using JobPlatform.AiMatching.Application.Interfaces;
using JobPlatform.AiMatching.Domain;
using JobPlatform.AiMatching.Domain.Interfaces.Repositories;
using JobPlatform.SharedKernel.Application.Interfaces.Cqrs;
using JobPlatform.SharedKernel.Application.Results;
using Microsoft.Extensions.Options;

namespace JobPlatform.AiMatching.Application.Handlers.Recommendations;

internal sealed class ComputeJobRecommendationHandler(IMatchScoreRepository scores, IKnownPostingRepository postings, IJobRecommendationRepository recommendations,
    IActivityHistory activity, IMatchingConfigurationProvider configuration, IOptions<MatchingOptions> options, TimeProvider clock)
    : ICommandHandler<ComputeJobRecommendationCommand, JobRecommendationDto>
{
    public async Task<Result<JobRecommendationDto>> Handle(ComputeJobRecommendationCommand request, CancellationToken ct)
    {
        var config = await configuration.GetAsync(ct);
        var candidates = new List<ScoredPosting>();
        var titles = new Dictionary<Guid, string>();
        foreach (var score in await scores.ListByProfileAsync(request.ProfileId, ct))
        {
            if (await postings.GetAsync(score.JobPostingId, ct) is { IsActive: true } posting)
            {
                titles[posting.Id] = posting.Title;
                candidates.Add(new ScoredPosting(posting.Id, score.Score, PreferenceFit(score)));
            }
        }

        var collaborative = await activity.GetCollaborativeScoresAsync(request.ProfileId, candidates.Select(c => c.JobPostingId).ToArray(), ct);
        var recommendation = JobRecommendation.Compute(request.ProfileId, candidates, collaborative, config.MatchThresholdPercent, options.Value.MaxRecommendations,
            request.ActorId, clock.GetUtcNow().UtcDateTime);
        recommendations.Add(recommendation);
        return new JobRecommendationDto(recommendation.Id, recommendation.Strategy.ToString(), recommendation.ComputedAtUtc,
            recommendation.Items.Select(i => new RecommendedJobDto(i.JobPostingId, titles.GetValueOrDefault(i.JobPostingId, string.Empty), i.Score, i.Reason.ToString())).ToArray());
    }

    /// <summary>How well the posting fits the stated location and salary preferences (0-1): the weaker of the two included criteria.</summary>
    private static decimal PreferenceFit(MatchScore score)
    {
        var relevant = score.Breakdown.Where(b => b.Included && b.Criterion is Criterion.Location or Criterion.Salary).Select(b => b.Score / 100m).ToArray();
        return relevant.Length == 0 ? 0m : relevant.Min();
    }
}
