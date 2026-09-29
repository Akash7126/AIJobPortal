using JobPlatform.JobPosting.Application.DTOs.Common;

namespace JobPlatform.JobPosting.Application.DTOs.SavedSearches;

public sealed record SavedSearchView(Guid SavedSearchId, SearchCriteriaInput Criteria, bool NotifyOnMatch, DateTime CreatedAtUtc, DateTime? LastEvaluatedAtUtc);
