using JobPlatform.JobSeekerProfile.Application.DTOs.Documents;

namespace JobPlatform.JobSeekerProfile.Application.Queries.Documents;

public sealed record ListSupplementaryDocumentsQuery : JobSeekerQuery<IReadOnlyList<DocumentView>>;
