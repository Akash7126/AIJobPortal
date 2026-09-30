namespace JobPlatform.HelpContent.Application.Interfaces;

/// <summary>Q-06 (proposed): BC-04's profile interest tags for news-feed personalisation (handover section 6.2), falling back to the
/// general feed when unavailable.</summary>
public interface IProfileInterestsProvider
{
    Task<IReadOnlyList<string>> GetInterestTagsAsync(Guid jobSeekerAccountId, CancellationToken ct = default);
}
