using JobPlatform.JobPosting.Application.DTOs.Favorites;

namespace JobPlatform.JobPosting.Application.Queries.Favorites;

public sealed record ListFavoritesQuery : JobSeekerQuery<FavoriteListView>;
