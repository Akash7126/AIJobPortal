namespace JobPlatform.JobPosting.Application.DTOs.Postings;

public sealed record JobVisibilityView(string Scope, IReadOnlyList<Guid> TargetJobSeekerIds);
