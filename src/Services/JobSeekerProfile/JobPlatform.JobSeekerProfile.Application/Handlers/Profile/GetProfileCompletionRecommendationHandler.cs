using JobPlatform.JobSeekerProfile.Application.DTOs.Profile;
using JobPlatform.JobSeekerProfile.Application.Queries.Profile;
using JobPlatform.JobSeekerProfile.Domain.Common;
using JobPlatform.JobSeekerProfile.Domain.Interfaces.Repositories;
using JobPlatform.SharedKernel.Application.Interfaces.Cqrs;
using JobPlatform.SharedKernel.Application.Interfaces.Ports;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.JobSeekerProfile.Application.Handlers.Profile;

internal sealed class GetProfileCompletionRecommendationHandler(IProfileRepository profiles, ICurrentUser user)
    : IQueryHandler<GetProfileCompletionRecommendationQuery, ProfileCompletionView>
{
    public async Task<Result<ProfileCompletionView>> Handle(GetProfileCompletionRecommendationQuery request, CancellationToken ct)
    {
        var profile = await profiles.GetByOwnerAsync(user.UserId!.Value, ct);
        if (profile is null)
        {
            return Error.NotFound(ErrorCodes.NotFound, "No profile exists for this account.");
        }

        var (percent, missing) = profile.ComputeCompletionRecommendation();
        return new ProfileCompletionView(percent, missing);
    }
}
