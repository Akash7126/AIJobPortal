using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.JobPosting.Application.Commands.Postings;

public sealed record UpdateJobPostingStatusCommand(Guid JobPostingId, string Status) : EmployerStatusCommand<Unit>;
