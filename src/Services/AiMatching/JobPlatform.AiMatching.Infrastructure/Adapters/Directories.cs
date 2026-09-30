using System.Security.Cryptography;
using JobPlatform.AiMatching.Application;
using JobPlatform.AiMatching.Application.DTOs.Semantics;
using JobPlatform.AiMatching.Application.Interfaces;
using JobPlatform.AiMatching.Domain;
using JobPlatform.BuildingBlocks.Infrastructure.Http;
using JobPlatform.SharedKernel.ApiContracts.Interfaces.JobPosting;
using JobPlatform.SharedKernel.ApiContracts.Interfaces.JobSeekerProfile;

namespace JobPlatform.AiMatching.Infrastructure.Adapters;

/// <summary>Translation of the other BCs' vocabulary into ours (education levels, work arrangements).</summary>
internal static class Vocabulary
{
    public static EducationLevel? Education(string? value) =>
        Enum.TryParse<EducationLevel>(value?.Trim(), true, out var level) ? level : null;

    public static WorkArrangement Arrangement(string? value) => (value ?? string.Empty).Trim().ToLowerInvariant() switch
    {
        "remote" or "online" or "work from home" => WorkArrangement.Remote,
        "hybrid" => WorkArrangement.Hybrid,
        _ => WorkArrangement.OnSite // onsite, physical, office, unknown
    };

    public static async Task<T> Guarded<T>(Func<Task<T>> call)
    {
        try
        {
            return await call();
        }
        catch (InternalApiException ex)
        {
            throw new UpstreamUnavailableException(ex.Message, ex);
        }
    }
}

/// <summary>ACL over BC-04: profile data as the scoring engine needs it.</summary>
internal sealed class ProfileDirectory(IJobSeekerProfileApi api) : IProfileDirectory
{
    public async Task<ProfileMatchView?> GetMatchViewAsync(Guid profileId, CancellationToken ct = default)
    {
        var dto = await Vocabulary.Guarded(() => api.GetProfileForMatchingAsync(profileId, ct));
        return dto is null || dto.Standing.Equals("Deactivated", StringComparison.OrdinalIgnoreCase)
            ? null
            : new ProfileMatchView(dto.ProfileId, dto.Version, dto.Skills, Vocabulary.Education(dto.EducationLevel), dto.Training, dto.Governorate, dto.City,
                dto.PreferredWorkArrangements.Select(a => Vocabulary.Arrangement(a)).Distinct().ToArray(), dto.YearsOfExperience, dto.ExpectedSalaryMin, dto.ExpectedSalaryMax);
    }

    public async Task<string?> GetEmbeddingTextAsync(Guid profileId, CancellationToken ct = default)
    {
        var dto = await Vocabulary.Guarded(() => api.GetProfileForMatchingAsync(profileId, ct));
        return dto is null
            ? null
            : string.Join(' ', new[] { dto.Headline }.Concat(dto.Skills).Concat(dto.Training).Concat(new[] { dto.EducationLevel, dto.City, dto.Governorate }).Where(t => !string.IsNullOrWhiteSpace(t)));
    }
}

/// <summary>ACL over BC-09: postings as matching input.</summary>
internal sealed class PostingDirectory(IJobPostingApi api) : IPostingDirectory
{
    public async Task<PostingSource?> GetAsync(Guid jobPostingId, CancellationToken ct = default)
    {
        var dto = await Vocabulary.Guarded(() => api.GetPostingForMatchingAsync(jobPostingId, ct));
        return dto is null
            ? null
            : new PostingSource(dto.JobPostingId, dto.EmployerAccountId, dto.Status, dto.Suspended, dto.Version, dto.Title, dto.Category, dto.Description, dto.Skills,
                Vocabulary.Education(dto.EducationLevel), dto.Training, dto.Governorate, dto.City, Vocabulary.Arrangement(dto.WorkArrangement), dto.MinExperienceYears,
                dto.MaxExperienceYears, dto.SalaryMin, dto.SalaryMax);
    }
}

/// <summary>Downloads the resume through BC-04's short-lived signed URL (never stored, never logged).</summary>
internal sealed class ResumeContentSource(IJobSeekerProfileApi api, IHttpClientFactory factory) : IResumeContentSource
{
    public const string ClientName = "resume-download";

    public async Task<ResumeContent?> FetchAsync(Guid resumeId, CancellationToken ct = default)
    {
        var link = await Vocabulary.Guarded(() => api.GetResumeContentUrlAsync(resumeId, ct));
        if (link is null)
        {
            return null;
        }

        if (link.SizeBytes > ResumeRules.MaxBytes)
        {
            return new ResumeContent(Array.Empty<byte>(), link.Format, link.Sha256, link.SizeBytes);
        }

        try
        {
            var bytes = await factory.CreateClient(ClientName).GetByteArrayAsync(link.Url, ct);
            return new ResumeContent(bytes, link.Format, Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant(), bytes.LongLength);
        }
        catch (HttpRequestException ex)
        {
            throw new UpstreamUnavailableException("The resume file could not be downloaded.", ex);
        }
    }
}
