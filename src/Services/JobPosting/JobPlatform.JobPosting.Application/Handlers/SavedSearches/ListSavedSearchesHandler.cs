using JobPlatform.JobPosting.Application.DTOs.SavedSearches;
using JobPlatform.JobPosting.Application.Queries.SavedSearches;
using JobPlatform.JobPosting.Domain;
using JobPlatform.SharedKernel.Application.Abstractions;
using JobPlatform.SharedKernel.Application.Ports;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.JobPosting.Application.Handlers.SavedSearches;

internal sealed class ListSavedSearchesHandler : IQueryHandler<ListSavedSearchesQuery, IReadOnlyList<SavedSearchView>>
{
    private readonly ISavedSearchRepository _searches;
    private readonly ICurrentUser _user;

    public ListSavedSearchesHandler(ISavedSearchRepository searches, ICurrentUser user)
    {
        _searches = searches;
        _user = user;
    }

    public async Task<Result<IReadOnlyList<SavedSearchView>>> Handle(ListSavedSearchesQuery request, CancellationToken ct)
    {
        var list = await _searches.ListByOwnerAsync(_user.UserId!.Value, ct);
        return Result.Success<IReadOnlyList<SavedSearchView>>(list.Select(SaveSearchHandler.ToView).ToArray());
    }
}
