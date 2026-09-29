using JobPlatform.JobSeekerProfile.Application.DTOs.Resume;
using JobPlatform.JobSeekerProfile.Application.Queries.Resume;
using JobPlatform.JobSeekerProfile.Domain;
using JobPlatform.JobSeekerProfile.Domain.Common;
using JobPlatform.SharedKernel.Application.Abstractions;
using JobPlatform.SharedKernel.Application.Ports;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.JobSeekerProfile.Application.Handlers.Resume;

internal sealed class GetResumeMetadataHandler(IProfileRepository profiles, IProfileReadStore reads, ICurrentUser user)
    : IQueryHandler<GetResumeMetadataQuery, ResumeView>
{
    public async Task<Result<ResumeView>> Handle(GetResumeMetadataQuery request, CancellationToken ct)
    {
        var profile = await profiles.GetByOwnerAsync(user.UserId!.Value, ct);
        if (profile is null)
        {
            return Error.NotFound(ErrorCodes.NotFound, "No profile exists for this account.");
        }

        var view = await reads.GetCurrentResumeAsync(profile.Id, ct);
        return view is null ? Error.NotFound(ErrorCodes.NotFound, "No resume has been uploaded.") : view;
    }
}
