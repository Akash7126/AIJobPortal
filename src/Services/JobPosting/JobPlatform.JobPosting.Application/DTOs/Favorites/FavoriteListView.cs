namespace JobPlatform.JobPosting.Application.DTOs.Favorites;

public sealed record FavoriteListView(Guid FavoriteJobListId, IReadOnlyList<Guid> JobPostingIds);
