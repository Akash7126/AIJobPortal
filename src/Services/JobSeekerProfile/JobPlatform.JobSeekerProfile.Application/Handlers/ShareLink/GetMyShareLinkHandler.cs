using JobPlatform.JobSeekerProfile.Application.DTOs.ShareLink;
using JobPlatform.JobSeekerProfile.Application.Queries.ShareLink;
using JobPlatform.JobSeekerProfile.Application.ShareLink;
using JobPlatform.JobSeekerProfile.Domain;
using JobPlatform.JobSeekerProfile.Domain.Common;
using JobPlatform.SharedKernel.Application.Abstractions;
using JobPlatform.SharedKernel.Application.Ports;
using JobPlatform.SharedKernel.Application.Results;
using Microsoft.Extensions.Options;

namespace JobPlatform.JobSeekerProfile.Application.Handlers.ShareLink;

internal sealed class GetMyShareLinkHandler(IProfileRepository profiles, IProfileShareLinkRepository links, ICurrentUser user,
    IOptions<PublicSiteOptions> options) : IQueryHandler<GetMyShareLinkQuery, ShareLinkView?>
{
    public async Task<Result<ShareLinkView?>> Handle(GetMyShareLinkQuery request, CancellationToken ct)
    {
        var profile = await profiles.GetByOwnerAsync(user.UserId!.Value, ct);
        if (profile is null)
        {
            return Error.NotFound(ErrorCodes.NotFound, "No profile exists for this account.");
        }

        var link = await links.GetActiveByProfileAsync(profile.Id, ct);
        return link is null ? Result.Success<ShareLinkView?>(null) : CreateProfileShareLinkHandler.ToView(link, options.Value);
    }
}
