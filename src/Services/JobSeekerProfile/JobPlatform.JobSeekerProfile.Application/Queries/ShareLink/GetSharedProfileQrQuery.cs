using JobPlatform.SharedKernel.Application.Abstractions;

namespace JobPlatform.JobSeekerProfile.Application.Queries.ShareLink;

public sealed record GetSharedProfileQrQuery(string Token) : IQuery<string>;
