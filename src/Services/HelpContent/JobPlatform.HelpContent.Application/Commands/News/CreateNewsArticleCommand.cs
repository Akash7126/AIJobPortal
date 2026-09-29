using JobPlatform.HelpContent.Application.DTOs.News;
using JobPlatform.HelpContent.Domain;

namespace JobPlatform.HelpContent.Application.Commands.News;

public sealed record CreateNewsArticleCommand(NewsKind Kind, string? TitleAr, string? TitleEn, string? BodyAr, string? BodyEn)
    : AdminCommand<NewsArticleMutationResult>;
