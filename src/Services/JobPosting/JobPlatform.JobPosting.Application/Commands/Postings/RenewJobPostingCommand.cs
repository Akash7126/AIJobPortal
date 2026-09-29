using JobPlatform.JobPosting.Application.DTOs.Postings;

namespace JobPlatform.JobPosting.Application.Commands.Postings;

public sealed record RenewJobPostingCommand(Guid JobPostingId, DateTime NewDeadlineUtc) : EmployerCommand<PostingMutationResult>;
