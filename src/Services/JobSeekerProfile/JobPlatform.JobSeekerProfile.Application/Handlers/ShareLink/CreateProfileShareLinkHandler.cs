using JobPlatform.JobSeekerProfile.Application.Commands.ShareLink;
using JobPlatform.JobSeekerProfile.Application.DTOs.ShareLink;
using JobPlatform.JobSeekerProfile.Application.ShareLink;
using JobPlatform.JobSeekerProfile.Domain.Common;
using JobPlatform.JobSeekerProfile.Domain.Interfaces.Repositories;
using JobPlatform.SharedKernel.Application.Interfaces.Cqrs;
using JobPlatform.SharedKernel.Application.Interfaces.Ports;
using JobPlatform.SharedKernel.Application.Results;
using Microsoft.Extensions.Options;

namespace JobPlatform.JobSeekerProfile.Application.Handlers.ShareLink;

internal sealed class CreateProfileShareLinkHandler(IProfileRepository profiles, IProfileShareLinkRepository links, IPrivacySettingRepository settings,
    ICurrentUser user, TimeProvider clock, IOptions<PublicSiteOptions> options) : ICommandHandler<CreateProfileShareLinkCommand, ShareLinkView>
{
    public async Task<Result<ShareLinkView>> Handle(CreateProfileShareLinkCommand request, CancellationToken ct)
    {
        var profile = await profiles.GetByOwnerAsync(user.UserId!.Value, ct);
        if (profile is null)
        {
            return Error.NotFound(ErrorCodes.NotFound, "No profile exists for this account.");
        }

        var existing = await links.GetActiveByProfileAsync(profile.Id, ct);
        if (existing is not null)
        {
            return ToView(existing, options.Value);
        }

        var setting = await settings.GetByProfileAsync(profile.Id, ct);
        var link = Domain.ProfileShareLink.Generate(Guid.NewGuid(), profile.Id, profile.OwnerAccountId, setting?.PublicSharingActive ?? false,
            clock.GetUtcNow().UtcDateTime);
        links.Add(link);
        return ToView(link, options.Value);
    }

    internal static ShareLinkView ToView(Domain.ProfileShareLink link, PublicSiteOptions options) =>
        new(link.Id, link.ProfileId, link.Token, $"{options.PublicBaseUrl}/shared/{link.Token}", link.IsActive, link.CreatedAtUtc);
}
