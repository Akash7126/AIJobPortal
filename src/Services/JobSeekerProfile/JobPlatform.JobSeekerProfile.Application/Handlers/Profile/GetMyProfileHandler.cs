using JobPlatform.JobSeekerProfile.Application.DTOs.Profile;
using JobPlatform.JobSeekerProfile.Application.Queries.Profile;
using JobPlatform.JobSeekerProfile.Domain.Common;
using JobPlatform.SharedKernel.Application.Abstractions;
using JobPlatform.SharedKernel.Application.Ports;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.JobSeekerProfile.Application.Handlers.Profile;

internal sealed class GetMyProfileHandler(IProfileReadStore reads, ICurrentUser user) : IQueryHandler<GetMyProfileQuery, ProfileView>
{
    public async Task<Result<ProfileView>> Handle(GetMyProfileQuery request, CancellationToken ct)
    {
        var view = await reads.GetByOwnerAsync(user.UserId!.Value, ct);
        return view is null ? Error.NotFound(ErrorCodes.NotFound, "No profile exists for this account.") : view;
    }
}
