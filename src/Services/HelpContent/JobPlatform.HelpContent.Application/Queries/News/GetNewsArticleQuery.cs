using JobPlatform.HelpContent.Application.DTOs.News;
using JobPlatform.SharedKernel.Application.Interfaces.Cqrs;

namespace JobPlatform.HelpContent.Application.Queries.News;

public sealed record GetNewsArticleQuery(Guid NewsArticleId) : IQuery<NewsArticleView>;
