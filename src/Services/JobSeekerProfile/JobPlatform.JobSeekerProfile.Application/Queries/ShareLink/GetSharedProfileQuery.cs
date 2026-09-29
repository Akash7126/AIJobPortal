using JobPlatform.JobSeekerProfile.Application.DTOs.ShareLink;
using JobPlatform.SharedKernel.Application.Abstractions;

namespace JobPlatform.JobSeekerProfile.Application.Queries.ShareLink;

/// <summary>Anonymous read by token (US-3.1.1-08): no <see cref="IAuthorizedRequest"/>, so the pipeline treats it as public.</summary>
public sealed record GetSharedProfileQuery(string Token) : IQuery<SharedProfileView>;
