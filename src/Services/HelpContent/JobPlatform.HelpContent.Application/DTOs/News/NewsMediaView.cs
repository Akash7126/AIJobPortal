namespace JobPlatform.HelpContent.Application.DTOs.News;

public sealed record NewsMediaView(Guid MediaId, string Type, string Url, string? AltText);
