using JobPlatform.HelpContent.Application.DTOs.News;
using JobPlatform.SharedKernel.Application.Abstractions;

namespace JobPlatform.HelpContent.Application.Queries.News;

public sealed record GetNewsArticleQuery(Guid NewsArticleId) : IQuery<NewsArticleView>;
