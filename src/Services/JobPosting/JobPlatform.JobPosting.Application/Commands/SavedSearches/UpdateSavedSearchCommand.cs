using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.JobPosting.Application.Commands.SavedSearches;

public sealed record UpdateSavedSearchCommand(Guid SavedSearchId, bool NotifyOnMatch) : JobSeekerCommand<Unit>;
