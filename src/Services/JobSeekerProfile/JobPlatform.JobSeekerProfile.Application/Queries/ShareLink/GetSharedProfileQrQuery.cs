using JobPlatform.SharedKernel.Application.Interfaces.Cqrs;

namespace JobPlatform.JobSeekerProfile.Application.Queries.ShareLink;

public sealed record GetSharedProfileQrQuery(string Token) : IQuery<string>;
