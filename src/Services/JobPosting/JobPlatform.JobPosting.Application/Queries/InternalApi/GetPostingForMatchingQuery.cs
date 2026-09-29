using JobPlatform.SharedKernel.ApiContracts.JobPosting;

namespace JobPlatform.JobPosting.Application.Queries.InternalApi;

/// <summary>Service-to-service queries behind <c>/internal/v1</c> (handover section 6.1); implements <see cref="IJobPostingApi"/> and the
/// reference-usage contract for BC-08.</summary>
public sealed record GetPostingForMatchingQuery(Guid JobPostingId) : ServiceQuery<PostingForMatchingDto?>;
