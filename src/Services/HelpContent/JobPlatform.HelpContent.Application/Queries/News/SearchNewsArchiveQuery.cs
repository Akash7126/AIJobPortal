using JobPlatform.HelpContent.Application.DTOs.News;
using JobPlatform.SharedKernel.Application.Interfaces.Cqrs;
using JobPlatform.SharedKernel.Application.Paging;

namespace JobPlatform.HelpContent.Application.Queries.News;

public sealed record SearchNewsArchiveQuery(string? Keyword, int Page = 1, int PageSize = 20) : IQuery<PagedResult<NewsListItemView>>;
