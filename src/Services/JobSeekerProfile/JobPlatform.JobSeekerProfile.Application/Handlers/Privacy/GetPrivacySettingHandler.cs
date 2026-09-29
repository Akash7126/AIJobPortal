using JobPlatform.JobSeekerProfile.Application.DTOs.Privacy;
using JobPlatform.JobSeekerProfile.Application.Queries.Privacy;
using JobPlatform.JobSeekerProfile.Domain;
using JobPlatform.JobSeekerProfile.Domain.Common;
using JobPlatform.SharedKernel.Application.Abstractions;
using JobPlatform.SharedKernel.Application.Ports;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.JobSeekerProfile.Application.Handlers.Privacy;

internal sealed class GetPrivacySettingHandler(IProfileRepository profiles, IPrivacySettingRepository settings, ICurrentUser user)
    : IQueryHandler<GetPrivacySettingQuery, PrivacySettingView>
{
    public async Task<Result<PrivacySettingView>> Handle(GetPrivacySettingQuery request, CancellationToken ct)
    {
        var profile = await profiles.GetByOwnerAsync(user.UserId!.Value, ct);
        if (profile is null)
        {
            return Error.NotFound(ErrorCodes.NotFound, "No profile exists for this account.");
        }

        var setting = await settings.GetByProfileAsync(profile.Id, ct) ?? PrivacySetting.CreateDefault(profile.Id);
        return new PrivacySettingView(setting.ProfileId, setting.Visibility.ToString(), setting.PublicSharingActive, setting.DeletionState.ToString(),
            setting.DeactivationRequestedAtUtc);
    }
}
