using JobPlatform.HelpContent.Application.DTOs.Help;
using JobPlatform.HelpContent.Application.Queries.Help;
using JobPlatform.SharedKernel.Application.Abstractions;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.HelpContent.Application.Handlers.Help;

internal sealed class ListHelpTopicsHandler : IQueryHandler<ListHelpTopicsQuery, IReadOnlyList<HelpTopicView>>
{
    private readonly IHelpContentReadStore _store;

    public ListHelpTopicsHandler(IHelpContentReadStore store) => _store = store;

    public async Task<Result<IReadOnlyList<HelpTopicView>>> Handle(ListHelpTopicsQuery request, CancellationToken ct) =>
        Result.Success(await _store.ListHelpTopicsAsync(ct));
}
