using JobPlatform.SharedKernel.ApiContracts.JobSeekerProfile;

namespace JobPlatform.CandidateSourcing.Application.Queries.Search;

/// <summary>US-3.3.3-05: the privacy-filtered view of one candidate. Pass-through to BC-04's own privacy-aware endpoint (it is the source of truth
/// for what a candidate has disclosed) - this BC never re-derives it from the projection replica.</summary>
public sealed record GetCandidateViewQuery(Guid CandidateProfileId) : EmployerQuery<CandidateViewDto>;
