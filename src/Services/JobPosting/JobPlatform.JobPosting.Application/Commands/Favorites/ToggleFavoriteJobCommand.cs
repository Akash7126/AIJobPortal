namespace JobPlatform.JobPosting.Application.Commands.Favorites;

/// <summary>US-3.2.2-03: heart-button toggle. Returns whether the posting is now favourited.</summary>
public sealed record ToggleFavoriteJobCommand(Guid JobPostingId) : JobSeekerCommand<bool>;
