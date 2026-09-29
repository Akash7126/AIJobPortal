using JobPlatform.JobPosting.Application.DTOs.Favorites;
using JobPlatform.JobPosting.Application.Queries.Favorites;
using JobPlatform.JobPosting.Domain;
using JobPlatform.SharedKernel.Application.Abstractions;
using JobPlatform.SharedKernel.Application.Ports;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.JobPosting.Application.Handlers.Favorites;

internal sealed class ListFavoritesHandler : IQueryHandler<ListFavoritesQuery, FavoriteListView>
{
    private readonly IFavoriteJobListRepository _favorites;
    private readonly ICurrentUser _user;

    public ListFavoritesHandler(IFavoriteJobListRepository favorites, ICurrentUser user)
    {
        _favorites = favorites;
        _user = user;
    }

    public async Task<Result<FavoriteListView>> Handle(ListFavoritesQuery request, CancellationToken ct)
    {
        var list = await _favorites.GetByOwnerAsync(_user.UserId!.Value, ct);
        return new FavoriteListView(list?.Id ?? Guid.Empty, list?.JobPostingIds ?? Array.Empty<Guid>());
    }
}
