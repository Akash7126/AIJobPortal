namespace JobPlatform.Notification.Application.DTOs.Admin;

public sealed record EmailTemplateDto(string Code, string Locale, int Version, string Subject, string Body, IReadOnlyDictionary<string, string> Placeholders);
