using JobPlatform.JobSeekerProfile.Application.Interfaces;
using JobPlatform.JobSeekerProfile.Application.Queries.ShareLink;
using JobPlatform.JobSeekerProfile.Application.ShareLink;
using JobPlatform.JobSeekerProfile.Domain.Common;
using JobPlatform.JobSeekerProfile.Domain.Interfaces.Repositories;
using JobPlatform.SharedKernel.Application.Interfaces.Cqrs;
using JobPlatform.SharedKernel.Application.Results;
using Microsoft.Extensions.Options;

namespace JobPlatform.JobSeekerProfile.Application.Handlers.ShareLink;

internal sealed class GetSharedProfileQrHandler(IProfileShareLinkRepository links, IQrCodeRenderer renderer, IOptions<PublicSiteOptions> options)
    : IQueryHandler<GetSharedProfileQrQuery, string>
{
    public async Task<Result<string>> Handle(GetSharedProfileQrQuery request, CancellationToken ct)
    {
        var link = await links.GetByTokenAsync(request.Token, ct);
        if (link is null || !link.IsActive)
        {
            return Error.NotFound(ErrorCodes.NotFound, "This share link is not active.");
        }

        return renderer.RenderSvg($"{options.Value.PublicBaseUrl}/shared/{link.Token}");
    }
}
