using JobPlatform.SharedKernel.ApiContracts.JobSeekerProfile;

namespace JobPlatform.SharedKernel.ApiContracts.Interfaces.JobSeekerProfile;

/// <summary>
/// Synchronous contract of BC-04 Job Seeker Profile for other BCs (routes under /internal/v1, service token with scope identity.internal).
/// Consumers: BC-10 (profiles/{id}, resumes/{id}/content-url) and BC-11 (profiles/{id}/privacy, profiles/{id}/candidate-view).
/// Shapes are derived from the BC-04 handover section 6.1 (sync-read US-3.3.1-01, US-3.3.3-05) and the BC-11 handover section 6.2.
/// </summary>
public interface IJobSeekerProfileApi
{
    /// <summary>GET /internal/v1/profiles/{id} - skills, education, experience, preferences for scoring. Null = 404.</summary>
    Task<ProfileForMatchingDto?> GetProfileForMatchingAsync(Guid profileId, CancellationToken ct = default);

    /// <summary>GET /internal/v1/resumes/{id}/content-url - short-lived signed URL of the resume file. Null = 404.</summary>
    Task<ResumeContentUrlDto?> GetResumeContentUrlAsync(Guid resumeId, CancellationToken ct = default);

    /// <summary>GET /internal/v1/profiles/{id}/privacy - candidate visibility. Null = 404.</summary>
    Task<CandidatePrivacyDto?> GetCandidatePrivacyAsync(Guid profileId, CancellationToken ct = default);

    /// <summary>GET /internal/v1/profiles/{id}/candidate-view - the fields visible under the candidate's current privacy settings. Null = 404.</summary>
    Task<CandidateViewDto?> GetCandidateViewAsync(Guid profileId, CancellationToken ct = default);
}
