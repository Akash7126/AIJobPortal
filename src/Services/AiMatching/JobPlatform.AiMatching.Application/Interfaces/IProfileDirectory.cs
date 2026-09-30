using JobPlatform.AiMatching.Domain;

namespace JobPlatform.AiMatching.Application.Interfaces;

/// <summary>BC-04 profile data for scoring. Null = the profile does not exist. Throws <see cref="UpstreamUnavailableException"/> when BC-04 cannot be reached.</summary>
public interface IProfileDirectory
{
    Task<ProfileMatchView?> GetMatchViewAsync(Guid profileId, CancellationToken ct = default);

    /// <summary>Text used to embed the profile (headline, skills, training, education, location).</summary>
    Task<string?> GetEmbeddingTextAsync(Guid profileId, CancellationToken ct = default);
}
