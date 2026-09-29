using JobPlatform.JobPosting.Application.DTOs.Common;

namespace JobPlatform.JobPosting.Application.DTOs.Interested;

public sealed record InterestedListItemView(Guid InterestedListEntryId, string ReferenceType, Guid? PostingId, SearchCriteriaInput? Criteria, DateTime CreatedAtUtc);
