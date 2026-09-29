using JobPlatform.AiMatching.Application.DTOs.Semantics;
using JobPlatform.AiMatching.Domain;

namespace JobPlatform.AiMatching.Application.Services.Semantics;

/// <summary>NLP analysis of a posting, its persisted semantics and its embedding in the vector index (US-3.3.1-03, handover 3.6).</summary>
public sealed class PostingAnalysisService(
    ISemanticAnalyzer analyzer, ISkillTaxonomyProvider taxonomies, IJobSemanticsRepository repository, IEmbeddingService embeddings, IVectorIndex index,
    TimeProvider clock)
{
    public async Task<JobSemantics> AnalyzeAsync(PostingSource source, CancellationToken ct)
    {
        var taxonomy = await taxonomies.GetAsync(null, ct);
        var analysis = await analyzer.AnalyzeAsync(source.Title, source.Description, source.Skills, source.Category, taxonomy, ct);
        var now = clock.GetUtcNow().UtcDateTime;
        var existing = await repository.GetAsync(source.JobPostingId, ct);
        JobSemantics semantics;
        if (existing is null)
        {
            semantics = JobSemantics.Analyze(source.JobPostingId, source.Version, analysis, now);
            repository.Add(semantics);
        }
        else
        {
            existing.Reanalyze(source.Version, analysis, now);
            semantics = existing;
        }

        // Only active postings are searchable candidates; anything else is removed from the index.
        if (source.IsActive)
        {
            var vector = await embeddings.EmbedAsync(PostingViews.EmbeddingText(source, semantics), ct);
            await index.UpsertAsync(VectorEntityType.Posting, source.JobPostingId, vector, embeddings.ModelVersion, ct);
        }
        else
        {
            await index.RemoveAsync(VectorEntityType.Posting, source.JobPostingId, ct);
        }

        return semantics;
    }
}
