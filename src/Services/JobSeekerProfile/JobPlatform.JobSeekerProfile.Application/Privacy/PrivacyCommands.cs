using JobPlatform.JobSeekerProfile.Domain;
using JobPlatform.JobSeekerProfile.Domain.Common;
using JobPlatform.SharedKernel.Application.Ports;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.JobSeekerProfile.Application.Privacy;

internal static class PrivacySupport
{
    public static async Task<Result<(Domain.Profile Profile, PrivacySetting Setting, bool IsNew)>> LoadAsync(IProfileRepository profiles,
        IPrivacySettingRepository settings, ICurrentUser user, CancellationToken ct)
    {
        var profile = await profiles.GetByOwnerAsync(user.UserId!.Value, ct);
        if (profile is null)
        {
            return Error.NotFound(ErrorCodes.NotFound, "No profile exists for this account.");
        }

        var setting = await settings.GetByProfileAsync(profile.Id, ct);
        var isNew = setting is null;
        setting ??= PrivacySetting.CreateDefault(profile.Id);
        setting.EnsureOwnedBy(new Actor(user.UserId!.Value), profile.OwnerAccountId);
        return (profile, setting, isNew);
    }
}
