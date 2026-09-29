namespace JobPlatform.JobSeekerProfile.Application.DTOs.ShareLink;

public sealed record ShareLinkView(Guid ShareLinkId, Guid ProfileId, string Token, string Url, bool IsActive, DateTime CreatedAtUtc);
