using JobPlatform.SharedKernel.ApiContracts.JobPosting;

namespace JobPlatform.SharedKernel.ApiContracts.Interfaces.JobPosting;

/// <summary>
/// Synchronous contract of BC-09 Job Posting for other BCs (routes under /internal/v1, service token with scope identity.internal).
/// Consumers: BC-10 (postings/{id}) and BC-11 (postings/{id}: ownership and active state).
/// </summary>
public interface IJobPostingApi
{
    /// <summary>GET /internal/v1/postings/{id}. Null = 404.</summary>
    Task<PostingForMatchingDto?> GetPostingForMatchingAsync(Guid jobPostingId, CancellationToken ct = default);
}
