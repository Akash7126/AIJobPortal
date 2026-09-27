using System.Security.Cryptography;
using System.Text;
using JobPlatform.AiMatching.Application;
using JobPlatform.AiMatching.Domain;
using Microsoft.EntityFrameworkCore;
using JobPlatform.AiMatching.Infrastructure.Persistence;

namespace JobPlatform.AiMatching.Infrastructure.Ai;

/// <summary>
/// Deterministic local embedding (feature hashing of words and character trigrams into a fixed-size, L2-normalised vector). Bilingual by construction:
/// text is normalised (case, Arabic diacritics and letter variants) and hashed on Unicode characters, so Arabic and English need no separate model.
/// Not a semantic model: near-synonyms only score high when they share characters. A real multilingual model plugs in behind <see cref="IEmbeddingService"/>.
/// </summary>
public sealed class HashingEmbeddingService : IEmbeddingService
{
    public const int Dimensions = 256;

    public string ModelVersion => "hashing-256-v1";

    public Task<float[]> EmbedAsync(string text, CancellationToken ct = default) => Task.FromResult(Embed(text));

    public static float[] Embed(string text)
    {
        var vector = new float[Dimensions];
        var normalized = TextNormalizer.Normalize(text);
        foreach (var word in normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            Add(vector, "w:" + word, 1.0f);
            var padded = $"^{word}$";
            for (var i = 0; i + 3 <= padded.Length; i++)
            {
                Add(vector, "t:" + padded.Substring(i, 3), 0.5f);
            }
        }

        var norm = MathF.Sqrt(vector.Sum(v => v * v));
        if (norm > 0)
        {
            for (var i = 0; i < vector.Length; i++)
            {
                vector[i] /= norm;
            }
        }

        return vector;
    }

    private static void Add(float[] vector, string feature, float weight)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(feature));
        var index = (int)(BitConverter.ToUInt32(hash, 0) % Dimensions);
        vector[index] += (hash[4] & 1) == 0 ? weight : -weight; // signed hashing keeps collisions unbiased
    }
}

/// <summary>Vector index over the matching.Embeddings table: exact cosine search in process (candidate sets are bounded by the retrieval limit). An ANN index can replace it behind <see cref="IVectorIndex"/> (Q-01).</summary>
internal sealed class EfVectorIndex(AiMatchingDbContext db, TimeProvider clock) : IVectorIndex
{
    public async Task UpsertAsync(VectorEntityType type, Guid entityId, float[] vector, string modelVersion, CancellationToken ct = default)
    {
        var kind = ToKind(type);
        var record = db.Embeddings.Local.FirstOrDefault(e => e.EntityType == kind && e.EntityId == entityId)
                     ?? await db.Embeddings.FirstOrDefaultAsync(e => e.EntityType == kind && e.EntityId == entityId, ct);
        if (record is null)
        {
            record = new EmbeddingRecord { EntityType = kind, EntityId = entityId };
            db.Embeddings.Add(record);
        }

        record.ModelVersion = modelVersion;
        record.Vector = ToBytes(vector);
        record.UpdatedAtUtc = clock.GetUtcNow().UtcDateTime;
    }

    public async Task RemoveAsync(VectorEntityType type, Guid entityId, CancellationToken ct = default)
    {
        var kind = ToKind(type);
        var record = db.Embeddings.Local.FirstOrDefault(e => e.EntityType == kind && e.EntityId == entityId)
                     ?? await db.Embeddings.FirstOrDefaultAsync(e => e.EntityType == kind && e.EntityId == entityId, ct);
        if (record is not null)
        {
            db.Embeddings.Remove(record);
        }
    }

    public async Task<IReadOnlyList<VectorHit>> SearchAsync(VectorEntityType type, float[] query, int topK, CancellationToken ct = default)
    {
        var kind = ToKind(type);
        var rows = await db.Embeddings.AsNoTracking().Where(e => e.EntityType == kind).Select(e => new { e.EntityId, e.Vector }).ToListAsync(ct);
        return rows.Select(r => new VectorHit(r.EntityId, VectorMath.Cosine(query, ToVector(r.Vector))))
            .OrderByDescending(h => h.Similarity).ThenBy(h => h.EntityId).Take(topK).ToArray();
    }

    private static VectorKind ToKind(VectorEntityType type) => type == VectorEntityType.Profile ? VectorKind.Profile : VectorKind.Posting;

    private static byte[] ToBytes(float[] vector)
    {
        var bytes = new byte[vector.Length * sizeof(float)];
        Buffer.BlockCopy(vector, 0, bytes, 0, bytes.Length);
        return bytes;
    }

    private static float[] ToVector(byte[] bytes)
    {
        var vector = new float[bytes.Length / sizeof(float)];
        Buffer.BlockCopy(bytes, 0, vector, 0, bytes.Length);
        return vector;
    }
}
