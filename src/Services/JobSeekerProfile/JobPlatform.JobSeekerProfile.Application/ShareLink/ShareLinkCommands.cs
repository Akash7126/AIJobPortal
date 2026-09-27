using JobPlatform.JobSeekerProfile.Domain;
using JobPlatform.JobSeekerProfile.Domain.Common;
using JobPlatform.SharedKernel.Application.Abstractions;
using JobPlatform.SharedKernel.Application.Ports;
using JobPlatform.SharedKernel.Application.Results;
using Microsoft.Extensions.Options;

namespace JobPlatform.JobSeekerProfile.Application.ShareLink;

/// <summary>Public site base URL used to build a share link (e.g. https://jobs.example) - Api:PublicBaseUrl in configuration.</summary>
public sealed class PublicSiteOptions
{
    public const string SectionName = "Api";

    public string PublicBaseUrl { get; set; } = "https://jobplatform.local";
}

public sealed record CreateProfileShareLinkCommand : JobSeekerCommand<ShareLinkView>;
public sealed record GetMyShareLinkQuery : JobSeekerQuery<ShareLinkView?>;

/// <summary>Anonymous read by token (US-3.1.1-08): no <see cref="IAuthorizedRequest"/>, so the pipeline treats it as public.</summary>
public sealed record GetSharedProfileQuery(string Token) : IQuery<SharedProfileView>;
public sealed record GetSharedProfileQrQuery(string Token) : IQuery<string>;

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
