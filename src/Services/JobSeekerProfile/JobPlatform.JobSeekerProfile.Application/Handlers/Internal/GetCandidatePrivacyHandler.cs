using JobPlatform.JobSeekerProfile.Application.Queries.Internal;
using JobPlatform.JobSeekerProfile.Domain;
using JobPlatform.JobSeekerProfile.Domain.Interfaces.Repositories;
using JobPlatform.SharedKernel.ApiContracts.JobSeekerProfile;
using JobPlatform.SharedKernel.Application.Interfaces.Cqrs;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.JobSeekerProfile.Application.Handlers.Internal;

internal sealed class GetCandidatePrivacyHandler(IProfileRepository profiles, IPrivacySettingRepository settings, TimeProvider clock)
    : IQueryHandler<GetCandidatePrivacyQuery, CandidatePrivacyDto?>
{
    public async Task<Result<CandidatePrivacyDto?>> Handle(GetCandidatePrivacyQuery request, CancellationToken ct)
    {
        var profile = await profiles.GetByIdAsync(request.ProfileId, ct);
        if (profile is null)
        {
            return Result.Success<CandidatePrivacyDto?>(null);
        }

        var setting = await settings.GetByProfileAsync(profile.Id, ct) ?? Domain.PrivacySetting.CreateDefault(profile.Id);
        return new CandidatePrivacyDto(profile.Id, setting.Visibility.ToString(), setting.PublicSharingActive, profile.Status == ProfileStatus.Deactivated,
            DisclosedFields(setting), clock.GetUtcNow().UtcDateTime);
    }

    private static IReadOnlyList<string> DisclosedFields(Domain.PrivacySetting setting) =>
        setting.Visibility == ProfileVisibility.Public || setting.PublicSharingActive
            ? new[] { "skills", "education", "experience", "location", "salary", "availability" }
            : Array.Empty<string>();
}
