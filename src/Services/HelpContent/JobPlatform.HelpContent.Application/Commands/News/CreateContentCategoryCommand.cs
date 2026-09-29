using JobPlatform.HelpContent.Application.DTOs.News;

namespace JobPlatform.HelpContent.Application.Commands.News;

public sealed record CreateContentCategoryCommand(string? NameAr, string? NameEn) : AdminCommand<ContentCategoryView>;
