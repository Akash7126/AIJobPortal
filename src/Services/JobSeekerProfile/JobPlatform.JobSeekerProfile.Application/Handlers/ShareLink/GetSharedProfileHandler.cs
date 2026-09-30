using JobPlatform.JobSeekerProfile.Application.DTOs.ShareLink;
using JobPlatform.JobSeekerProfile.Application.Interfaces;
using JobPlatform.JobSeekerProfile.Application.Queries.ShareLink;
using JobPlatform.JobSeekerProfile.Domain.Common;
using JobPlatform.JobSeekerProfile.Domain.Interfaces.Repositories;
using JobPlatform.SharedKernel.Application.Interfaces.Cqrs;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.JobSeekerProfile.Application.Handlers.ShareLink;

internal sealed class GetSharedProfileHandler(IProfileShareLinkRepository links, IProfileReadStore reads) : IQueryHandler<GetSharedProfileQuery, SharedProfileView>
{
    public async Task<Result<SharedProfileView>> Handle(GetSharedProfileQuery request, CancellationToken ct)
    {
        var link = await links.GetByTokenAsync(request.Token, ct);
        if (link is null || !link.IsActive)
        {
            return Error.NotFound(ErrorCodes.NotFound, "This share link is not active.");
        }

        var view = await reads.GetSharedAsync(link.ProfileId, ct);
        return view is null ? Error.NotFound(ErrorCodes.NotFound, "This share link is not active.") : view;
    }
}
