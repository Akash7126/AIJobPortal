namespace JobPlatform.AiMatching.Application.Interfaces;

/// <summary>Activity history (views, favourites, saves) for collaborative filtering. No BC publishes it today (Q-06): the default adapter returns nothing, so recommendations are content-only.</summary>
public interface IActivityHistory
{
    /// <summary>Collaborative score 0-100 per posting for this profile; empty when there is not enough history.</summary>
    Task<IReadOnlyDictionary<Guid, decimal>> GetCollaborativeScoresAsync(Guid profileId, IReadOnlyCollection<Guid> candidatePostingIds, CancellationToken ct = default);
}
