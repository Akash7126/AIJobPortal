using JobPlatform.SharedKernel.ApiContracts.JobSeekerProfile;

namespace JobPlatform.JobSeekerProfile.Application.Queries.Internal;

public sealed record GetResumeContentUrlQuery(Guid ResumeId) : ServiceQuery<ResumeContentUrlDto?>;
