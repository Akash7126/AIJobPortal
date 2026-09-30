using JobPlatform.HelpContent.Application.DTOs.News;
using JobPlatform.HelpContent.Application.Interfaces;
using JobPlatform.HelpContent.Application.Queries.News;
using JobPlatform.HelpContent.Domain;
using JobPlatform.SharedKernel.Application.Interfaces.Cqrs;
using JobPlatform.SharedKernel.Application.Paging;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.HelpContent.Application.Handlers.News;

internal sealed class ListNewsHandler : IQueryHandler<ListNewsQuery, PagedResult<NewsListItemView>>
{
    private readonly IHelpContentReadStore _store;

    public ListNewsHandler(IHelpContentReadStore store) => _store = store;

    public async Task<Result<PagedResult<NewsListItemView>>> Handle(ListNewsQuery request, CancellationToken ct) =>
        await _store.ListNewsAsync(request.CategoryId, NewsStatus.Published.ToString(), new PageRequest(request.Page, request.PageSize), ct);
}
