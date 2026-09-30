namespace JobPlatform.AiMatching.Application.Interfaces;

public interface IEmbeddingService
{
    string ModelVersion { get; }

    /// <summary>L2-normalised embedding; deterministic for the same text and model version.</summary>
    Task<float[]> EmbedAsync(string text, CancellationToken ct = default);
}
