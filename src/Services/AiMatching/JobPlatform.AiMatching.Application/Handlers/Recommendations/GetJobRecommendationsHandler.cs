using JobPlatform.AiMatching.Application.Commands.Recommendations;
using JobPlatform.AiMatching.Application.DTOs.Recommendations;
using JobPlatform.AiMatching.Application.Queries.Recommendations;
using JobPlatform.AiMatching.Domain;
using JobPlatform.SharedKernel.Application.Abstractions;
using JobPlatform.SharedKernel.Application.Ports;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.AiMatching.Application.Handlers.Recommendations;

internal sealed class GetJobRecommendationsHandler(IKnownProfileRepository profiles, IMatchReadStore store, ISender sender, ICurrentUser user)
    : IQueryHandler<GetJobRecommendationsQuery, JobRecommendationDto>
{
    public async Task<Result<JobRecommendationDto>> Handle(GetJobRecommendationsQuery request, CancellationToken ct)
    {
        var profile = user.UserId is { } account ? await profiles.GetByOwnerAsync(account, ct) : null;
        if (profile is null)
        {
            return new JobRecommendationDto(Guid.Empty, RecommendationStrategy.ContentOnly.ToString(), default, Array.Empty<RecommendedJobDto>());
        }

        if (await store.GetLatestRecommendationAsync(profile.Id, ct) is { } latest)
        {
            return latest;
        }

        return await sender.Send(new ComputeJobRecommendationCommand(profile.Id, user.UserId!.Value), ct);
    }
}
