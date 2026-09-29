namespace JobPlatform.HelpContent.Application.DTOs.CompanyPage;

public sealed record OpenPostingView(Guid JobPostingId, string Title, string? Governorate, string? City, DateTime DeadlineUtc);
