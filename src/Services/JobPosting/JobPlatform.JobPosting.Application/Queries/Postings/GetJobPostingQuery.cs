using JobPlatform.JobPosting.Application.DTOs.Postings;

namespace JobPlatform.JobPosting.Application.Queries.Postings;

public sealed record GetJobPostingQuery(Guid JobPostingId) : PublicQuery<JobPostingView>;
