using JobPlatform.BuildingBlocks.Infrastructure.Http;
using JobPlatform.SharedKernel.ApiContracts.Interfaces.JobPosting;
using JobPlatform.SharedKernel.ApiContracts.Interfaces.JobSeekerProfile;
using JobPlatform.SharedKernel.ApiContracts.Interfaces.PlatformAdministration;
using JobPlatform.SharedKernel.ApiContracts.JobPosting;
using JobPlatform.SharedKernel.ApiContracts.JobSeekerProfile;
using JobPlatform.SharedKernel.ApiContracts.PlatformAdministration;

namespace JobPlatform.AiMatching.Infrastructure.Clients;

// Typed HTTP clients of the consumed internal APIs (foundation section 9.5): service token, 2 s timeout, GET retries. They speak the SharedKernel contract only;
// translation into this BC's language happens in the anti-corruption adapters (Adapters/).

internal sealed class JobSeekerProfileApiClient(HttpClient http) : IJobSeekerProfileApi
{
    public Task<ProfileForMatchingDto?> GetProfileForMatchingAsync(Guid profileId, CancellationToken ct = default) =>
        http.GetOrNullAsync<ProfileForMatchingDto>($"internal/v1/profiles/{profileId}", ct);

    public Task<ResumeContentUrlDto?> GetResumeContentUrlAsync(Guid resumeId, CancellationToken ct = default) =>
        http.GetOrNullAsync<ResumeContentUrlDto>($"internal/v1/resumes/{resumeId}/content-url", ct);

    public Task<CandidatePrivacyDto?> GetCandidatePrivacyAsync(Guid profileId, CancellationToken ct = default) =>
        http.GetOrNullAsync<CandidatePrivacyDto>($"internal/v1/profiles/{profileId}/privacy", ct);

    public Task<CandidateViewDto?> GetCandidateViewAsync(Guid profileId, CancellationToken ct = default) =>
        http.GetOrNullAsync<CandidateViewDto>($"internal/v1/profiles/{profileId}/candidate-view", ct);
}

internal sealed class JobPostingApiClient(HttpClient http) : IJobPostingApi
{
    public Task<PostingForMatchingDto?> GetPostingForMatchingAsync(Guid jobPostingId, CancellationToken ct = default) =>
        http.GetOrNullAsync<PostingForMatchingDto>($"internal/v1/postings/{jobPostingId}", ct);
}

internal sealed class PlatformAdministrationApiClient(HttpClient http) : IPlatformAdministrationApi
{
    public Task<TaxonomyDto?> GetTaxonomyAsync(string type, string? version, CancellationToken ct = default) =>
        http.GetOrNullAsync<TaxonomyDto>($"internal/v1/taxonomies/{Uri.EscapeDataString(type)}" + (version is null ? string.Empty : $"?version={Uri.EscapeDataString(version)}"), ct);
}
