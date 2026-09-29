using JobPlatform.JobPosting.Application.DTOs.Interested;

namespace JobPlatform.JobPosting.Application.Queries.Interested;

public sealed record ListInterestedListQuery : JobSeekerQuery<IReadOnlyList<InterestedListItemView>>;
