using JobPlatform.SharedKernel.ApiContracts.JobSeekerProfile;

namespace JobPlatform.JobSeekerProfile.Application.Queries.Internal;

public sealed record GetCandidatePrivacyQuery(Guid ProfileId) : ServiceQuery<CandidatePrivacyDto?>;
