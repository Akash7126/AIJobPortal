using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.JobPosting.Application.Commands.SavedSearches;

public sealed record DeleteSavedSearchCommand(Guid SavedSearchId) : JobSeekerCommand<Unit>;
