namespace JobPlatform.JobPosting.Application.DTOs.Postings;

/// <summary>
/// Result of a posting mutation. Deliberately does not carry the aggregate's RowVersion: a SQL Server rowversion (and the SQLite-stamped
/// equivalent) is only assigned when SaveChanges runs, which happens after the handler returns inside the same command pipeline
/// (foundation section 6) - any byte[] captured here would be the pre-save value, not the true new ETag. Callers that need the current
/// ETag re-read it from <see cref="JobPostingView"/> (GetJobPostingQuery), which always reflects the committed state.
/// </summary>
public sealed record PostingMutationResult(Guid JobPostingId, bool Existing = false, bool Overwritten = false);
