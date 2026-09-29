using JobPlatform.JobPosting.Application.DTOs.SavedSearches;

namespace JobPlatform.JobPosting.Application.Queries.SavedSearches;

public sealed record ListSavedSearchesQuery : JobSeekerQuery<IReadOnlyList<SavedSearchView>>;
