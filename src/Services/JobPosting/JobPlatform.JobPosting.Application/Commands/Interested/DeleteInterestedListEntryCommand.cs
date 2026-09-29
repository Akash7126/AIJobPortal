using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.JobPosting.Application.Commands.Interested;

public sealed record DeleteInterestedListEntryCommand(Guid InterestedListEntryId) : JobSeekerInterestedCommand<Unit>;
