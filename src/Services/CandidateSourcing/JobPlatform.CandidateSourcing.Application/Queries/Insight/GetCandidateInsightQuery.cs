using JobPlatform.CandidateSourcing.Application.DTOs.Insight;
using JobPlatform.SharedKernel.Application.Interfaces.Persistence;

namespace JobPlatform.CandidateSourcing.Application.Queries.Insight;

/// <summary>
/// US-3.3.3-06 (implemented here per the handover's Q-01 recommendation): availability, salary expectation and fit for one candidate against one
/// posting. Computed fresh on every read (no story requires caching it) and persisted as a CandidateInsight row so CandidateInsightComputed is
/// published (BC-07/BC-12 audit trail) - a query handler may write here because it is not wrapped by the automatic unit of work, so it commits
/// explicitly through <see cref="IUnitOfWork"/>, which the outbox interceptor still observes.
/// </summary>
public sealed record GetCandidateInsightQuery(Guid CandidateProfileId, Guid JobPostingId) : EmployerQuery<CandidateInsightView>;
