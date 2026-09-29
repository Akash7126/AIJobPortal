using JobPlatform.HelpContent.Application.DTOs.News;
using JobPlatform.SharedKernel.Application.Abstractions;
using JobPlatform.SharedKernel.Application.Paging;

namespace JobPlatform.HelpContent.Application.Queries.News;

public sealed record ListNewsQuery(Guid? CategoryId, int Page = 1, int PageSize = 20) : IQuery<PagedResult<NewsListItemView>>;
