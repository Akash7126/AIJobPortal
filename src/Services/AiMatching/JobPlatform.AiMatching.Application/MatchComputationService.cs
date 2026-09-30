using JobPlatform.AiMatching.Application.DTOs.Semantics;
using JobPlatform.AiMatching.Application.Interfaces;
using JobPlatform.AiMatching.Domain;

namespace JobPlatform.AiMatching.Application;

public static class VectorMath
{
    /// <summary>Cosine similarity of two vectors (vectors from <see cref="IEmbeddingService"/> are L2-normalised, so this is the dot product; safe for others too).</summary>
    public static double Cosine(float[] a, float[] b)
    {
        double dot = 0, na = 0, nb = 0;
        var n = Math.Min(a.Length, b.Length);
        for (var i = 0; i < n; i++)
        {
            dot += a[i] * b[i];
            na += a[i] * a[i];
            nb += b[i] * b[i];
        }

        return na == 0 || nb == 0 ? 0 : dot / Math.Sqrt(na * nb);
    }
}

public static class PostingViews
{
    /// <summary>The posting as the scoring engine sees it: BC-09's fields plus the skills the NLP extracted (US-3.3.1-03: attached to the posting for matching).</summary>
    public static PostingMatchView ToView(PostingSource source, JobSemantics? semantics) => new(
        source.JobPostingId, source.Version, source.IsActive,
        source.Skills.Concat(semantics?.RequiredSkills ?? Array.Empty<string>()).Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
        source.EducationLevel, source.Training, source.Governorate, source.City, source.Arrangement, source.MinExperienceYears, source.MaxExperienceYears,
        source.SalaryMin, source.SalaryMax);

    public static string EmbeddingText(PostingSource source, JobSemantics? semantics) =>
        string.Join(' ', new[] { source.Title, source.Category, source.Description }.Concat(source.Skills).Concat(semantics?.RequiredSkills ?? Array.Empty<string>()))
            .Trim();
}

public sealed record MatchOutcome(decimal Score, MatchScore? Stored);
