using JobPlatform.HelpContent.Application.DTOs.News;

namespace JobPlatform.HelpContent.Application.Commands.News;

public sealed record EditNewsArticleCommand(Guid NewsArticleId, string? TitleAr, string? TitleEn, string? BodyAr, string? BodyEn)
    : AdminCommand<NewsArticleView>;
