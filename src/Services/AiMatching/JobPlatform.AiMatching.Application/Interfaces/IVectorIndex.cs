namespace JobPlatform.AiMatching.Application.Interfaces;

public interface IVectorIndex
{
    Task UpsertAsync(VectorEntityType type, Guid entityId, float[] vector, string modelVersion, CancellationToken ct = default);

    Task RemoveAsync(VectorEntityType type, Guid entityId, CancellationToken ct = default);

    /// <summary>Nearest neighbours by cosine similarity, best first.</summary>
    Task<IReadOnlyList<VectorHit>> SearchAsync(VectorEntityType type, float[] query, int topK, CancellationToken ct = default);
}
