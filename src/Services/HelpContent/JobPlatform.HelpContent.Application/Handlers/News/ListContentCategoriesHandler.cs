using JobPlatform.HelpContent.Application.DTOs.News;
using JobPlatform.HelpContent.Application.Interfaces;
using JobPlatform.HelpContent.Application.Queries.News;
using JobPlatform.SharedKernel.Application.Interfaces.Cqrs;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.HelpContent.Application.Handlers.News;

internal sealed class ListContentCategoriesHandler : IQueryHandler<ListContentCategoriesQuery, IReadOnlyList<ContentCategoryView>>
{
    private readonly IHelpContentReadStore _store;

    public ListContentCategoriesHandler(IHelpContentReadStore store) => _store = store;

    public async Task<Result<IReadOnlyList<ContentCategoryView>>> Handle(ListContentCategoriesQuery request, CancellationToken ct) =>
        Result.Success(await _store.ListCategoriesAsync(ct));
}
