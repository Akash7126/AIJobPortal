using JobPlatform.HelpContent.Application.DTOs.News;
using JobPlatform.HelpContent.Application.Queries.News;
using JobPlatform.HelpContent.Domain;
using JobPlatform.HelpContent.Domain.Common;
using JobPlatform.SharedKernel.Application.Abstractions;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.HelpContent.Application.Handlers.News;

internal sealed class GetNewsArticleHandler : IQueryHandler<GetNewsArticleQuery, NewsArticleView>
{
    private readonly IHelpContentReadStore _store;

    public GetNewsArticleHandler(IHelpContentReadStore store) => _store = store;

    public async Task<Result<NewsArticleView>> Handle(GetNewsArticleQuery request, CancellationToken ct)
    {
        var view = await _store.GetNewsArticleAsync(request.NewsArticleId, ct);
        // A draft is not yet public content (handover section 2: "Draft" is not one of the reader-visible states).
        return view is null || view.Status == NewsStatus.Draft.ToString()
            ? Error.NotFound(ErrorCodes.NotFound, "The news article was not found.")
            : view;
    }
}
