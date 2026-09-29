using JobPlatform.HelpContent.Application.DTOs.News;
using JobPlatform.HelpContent.Domain;

namespace JobPlatform.HelpContent.Application.Commands.News;

public sealed record AddNewsMediaCommand(Guid NewsArticleId, NewsMediaType Type, string FileName, string ContentType, long SizeBytes, byte[] Content,
    string? AltText) : AdminCommand<NewsMediaView>;
