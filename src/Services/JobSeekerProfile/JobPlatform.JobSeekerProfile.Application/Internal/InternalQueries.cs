using JobPlatform.JobSeekerProfile.Domain;
using JobPlatform.JobSeekerProfile.Domain.Common;
using JobPlatform.SharedKernel.ApiContracts.JobSeekerProfile;
using JobPlatform.SharedKernel.Application.Abstractions;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.JobSeekerProfile.Application.Internal;

/// <summary>Implements the SharedKernel IJobSeekerProfileApi contract (handover section 6.1 internal routes) over the request pipeline.</summary>
public sealed record GetProfileForMatchingQuery(Guid ProfileId) : ServiceQuery<ProfileForMatchingDto?>;
public sealed record GetCandidatePrivacyQuery(Guid ProfileId) : ServiceQuery<CandidatePrivacyDto?>;
public sealed record GetCandidateViewQuery(Guid ProfileId) : ServiceQuery<CandidateViewDto?>;
public sealed record GetResumeContentUrlQuery(Guid ResumeId) : ServiceQuery<ResumeContentUrlDto?>;

internal sealed class GetProfileForMatchingHandler(IProfileRepository profiles) : IQueryHandler<GetProfileForMatchingQuery, ProfileForMatchingDto?>
{
    public async Task<Result<ProfileForMatchingDto?>> Handle(GetProfileForMatchingQuery request, CancellationToken ct)
    {
        var profile = await profiles.GetByIdAsync(request.ProfileId, ct);
        if (profile is null)
        {
            return Result.Success<ProfileForMatchingDto?>(null);
        }

        return new ProfileForMatchingDto(profile.Id, profile.OwnerAccountId, profile.Status.ToString(), profile.Version, null,
            profile.Skills.Select(s => s.Name).ToList(), profile.Education.OrderByDescending(e => e.To).FirstOrDefault()?.Degree,
            profile.Training.Select(t => t.Name).ToList(), profile.Address?.Governorate, profile.Address?.City, Array.Empty<string>(),
            profile.YearsOfExperience, profile.SalaryExpectation?.Min, profile.SalaryExpectation?.Max);
    }
}

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

internal sealed class GetResumeContentUrlHandler(IResumeRepository resumes, JobPlatform.JobSeekerProfile.Application.IFileStorage storage)
    : IQueryHandler<GetResumeContentUrlQuery, ResumeContentUrlDto?>
{
    public async Task<Result<ResumeContentUrlDto?>> Handle(GetResumeContentUrlQuery request, CancellationToken ct)
    {
        var resume = await resumes.GetByIdAsync(request.ResumeId, ct);
        if (resume is null)
        {
            return Result.Success<ResumeContentUrlDto?>(null);
        }

        var (url, expires) = await storage.GetSignedUrlAsync(resume.File.StorageKey, TimeSpan.FromMinutes(15), ct);
        return new ResumeContentUrlDto(resume.Id, resume.ProfileId, url, resume.Format.ToString(), resume.File.SizeBytes, resume.File.Sha256, expires);
    }
}
