using JobPlatform.JobSeekerProfile.Application.Queries.Internal;
using JobPlatform.JobSeekerProfile.Domain;
using JobPlatform.JobSeekerProfile.Domain.Interfaces.Repositories;
using JobPlatform.SharedKernel.ApiContracts.JobSeekerProfile;
using JobPlatform.SharedKernel.Application.Interfaces.Cqrs;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.JobSeekerProfile.Application.Handlers.Internal;

internal sealed class GetCandidateViewHandler(IProfileRepository profiles, IPrivacySettingRepository settings, TimeProvider clock)
    : IQueryHandler<GetCandidateViewQuery, CandidateViewDto?>
{
    public async Task<Result<CandidateViewDto?>> Handle(GetCandidateViewQuery request, CancellationToken ct)
    {
        var profile = await profiles.GetByIdAsync(request.ProfileId, ct);
        if (profile is null)
        {
            return Result.Success<CandidateViewDto?>(null);
        }

        var setting = await settings.GetByProfileAsync(profile.Id, ct) ?? Domain.PrivacySetting.CreateDefault(profile.Id);
        var disclosed = setting.Visibility == ProfileVisibility.Public || setting.PublicSharingActive;
        var fields = disclosed
            ? new[] { "skills", "education", "experience", "location", "salary", "availability" }
            : Array.Empty<string>();

        return new CandidateViewDto(profile.Id, setting.Visibility.ToString(), setting.PublicSharingActive, profile.Status == ProfileStatus.Deactivated,
            fields, disclosed ? profile.Skills.Select(s => s.Name).ToList() : Array.Empty<string>(),
            disclosed ? profile.Education.OrderByDescending(e => e.To).FirstOrDefault()?.Degree : null, disclosed ? profile.YearsOfExperience : null,
            disclosed ? profile.Address?.Governorate : null, Array.Empty<string>(), disclosed ? profile.SalaryExpectation?.Min : null,
            disclosed ? profile.SalaryExpectation?.Max : null, null, clock.GetUtcNow().UtcDateTime);
    }
}
