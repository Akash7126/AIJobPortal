using JobPlatform.SharedKernel.ApiContracts.JobSeekerProfile;

namespace JobPlatform.JobSeekerProfile.Application.Queries.Internal;

/// <summary>Implements the SharedKernel IJobSeekerProfileApi contract (handover section 6.1 internal routes) over the request pipeline.</summary>
public sealed record GetProfileForMatchingQuery(Guid ProfileId) : ServiceQuery<ProfileForMatchingDto?>;
