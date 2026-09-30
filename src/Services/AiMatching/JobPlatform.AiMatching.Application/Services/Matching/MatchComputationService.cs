using JobPlatform.AiMatching.Application.DTOs.Semantics;
using JobPlatform.AiMatching.Application.Interfaces;
using JobPlatform.AiMatching.Application.Services.Semantics;
using JobPlatform.AiMatching.Domain;
using JobPlatform.AiMatching.Domain.Interfaces.Repositories;
using JobPlatform.AiMatching.Domain.Interfaces.Services;
using Microsoft.Extensions.Options;

namespace JobPlatform.AiMatching.Application.Services.Matching;

/// <summary>
/// Orchestrates scoring: builds the views (ACL ports), enriches skill comparison with embedding similarity, runs the pure engine with the configuration snapshot
/// taken at the start, and applies the storage policy (D-01: persist only scores at or above threshold minus margin).
/// </summary>
public sealed class MatchComputationService(
    IProfileDirectory profiles, IPostingDirectory postings, IJobSemanticsRepository semanticsRepository, IEmbeddingService embeddings, IVectorIndex index,
    IMatchScoreRepository scores, IKnownProfileRepository knownProfiles, IKnownPostingRepository knownPostings, IMatchingConfigurationProvider configuration,
    PostingAnalysisService analysis, IOptions<MatchingOptions> options, TimeProvider clock)
{
    private readonly Dictionary<string, float[]> _termVectors = new(StringComparer.Ordinal);

    public async Task<(PostingSource Source, PostingMatchView View)?> LoadPostingAsync(Guid postingId, bool refreshSemantics, CancellationToken ct)
    {
        var source = await postings.GetAsync(postingId, ct);
        if (source is null)
        {
            return null;
        }

        var semantics = refreshSemantics ? await analysis.AnalyzeAsync(source, ct) : await semanticsRepository.GetAsync(postingId, ct);
        return (source, PostingViews.ToView(source, semantics));
    }

    /// <summary>Builds the pairwise similarity of the two term sets from embeddings: a near-synonym counts as a (weaker) overlap.</summary>
    public async Task<ISkillSimilarity> SimilarityAsync(ProfileMatchView profile, PostingMatchView posting, CancellationToken ct)
    {
        var threshold = options.Value.SynonymSimilarityThreshold;
        var table = new Dictionary<(string, string), double>();
        var offered = profile.Skills.Concat(profile.Training).Select(TextNormalizer.Normalize).Where(t => t.Length > 0).Distinct().ToArray();
        var required = posting.RequiredSkills.Concat(posting.RequiredTraining).Select(TextNormalizer.Normalize).Where(t => t.Length > 0).Distinct().ToArray();
        foreach (var r in required)
        {
            foreach (var p in offered.Where(p => p != r))
            {
                var similarity = VectorMath.Cosine(await VectorOfAsync(r, ct), await VectorOfAsync(p, ct));
                if (similarity >= threshold)
                {
                    table[(r, p)] = similarity;
                }
            }
        }

        return new TableSkillSimilarity(table);
    }

    private async Task<float[]> VectorOfAsync(string term, CancellationToken ct)
    {
        if (!_termVectors.TryGetValue(term, out var vector))
        {
            _termVectors[term] = vector = await embeddings.EmbedAsync(term, ct);
        }

        return vector;
    }

    /// <summary>Scores one pair and persists it when it is at or above (threshold - margin). An existing stored score is refreshed (INV-06: unchanged inputs change nothing).</summary>
    public async Task<MatchOutcome> ScoreAsync(ProfileMatchView profile, PostingMatchView posting, MatchingConfigSnapshot config, CancellationToken ct)
    {
        var similarity = await SimilarityAsync(profile, posting, ct);
        var now = clock.GetUtcNow().UtcDateTime;
        var existing = await scores.GetPairAsync(profile.ProfileId, posting.PostingId, ct);
        if (existing is not null)
        {
            existing.Recompute(profile, posting, config, similarity, embeddings.ModelVersion, now);
            return new MatchOutcome(existing.Score, existing);
        }

        var created = MatchScore.Compute(profile, posting, config, similarity, embeddings.ModelVersion, now);
        if (created.Score < config.MatchThresholdPercent - options.Value.StorageMarginPercent)
        {
            return new MatchOutcome(created.Score, null);
        }

        scores.Add(created);
        return new MatchOutcome(created.Score, created);
    }

    /// <summary>Candidate retrieval for a posting: nearest profile embeddings, scored and stored. Returns how many scores were stored or refreshed.</summary>
    public async Task<int> ComputeForPostingAsync(Guid postingId, CancellationToken ct)
    {
        var config = await configuration.GetAsync(ct);
        var loaded = await LoadPostingAsync(postingId, refreshSemantics: true, ct);
        if (loaded is null || !loaded.Value.View.IsActive)
        {
            await scores.RemoveByPostingAsync(postingId, ct);
            await index.RemoveAsync(VectorEntityType.Posting, postingId, ct);
            return 0;
        }

        var (source, view) = loaded.Value;
        var semantics = await semanticsRepository.GetAsync(postingId, ct);
        var query = await embeddings.EmbedAsync(PostingViews.EmbeddingText(source, semantics), ct);
        var count = 0;
        foreach (var hit in await index.SearchAsync(VectorEntityType.Profile, query, options.Value.CandidateRetrievalLimit, ct))
        {
            var known = await knownProfiles.GetAsync(hit.EntityId, ct);
            if (known is not { Standing: KnownStanding.Active })
            {
                continue;
            }

            if (await profiles.GetMatchViewAsync(hit.EntityId, ct) is { } profile && (await ScoreAsync(profile, view, config, ct)).Stored is not null)
            {
                count++;
            }
        }

        return count;
    }

    /// <summary>Candidate retrieval for a profile: nearest posting embeddings that are still active, scored and stored.</summary>
    public async Task<int> ComputeForProfileAsync(Guid profileId, CancellationToken ct)
    {
        var config = await configuration.GetAsync(ct);
        var profile = await profiles.GetMatchViewAsync(profileId, ct);
        var text = await profiles.GetEmbeddingTextAsync(profileId, ct);
        if (profile is null || text is null)
        {
            return 0;
        }

        var query = await embeddings.EmbedAsync(text, ct);
        var count = 0;
        foreach (var hit in await index.SearchAsync(VectorEntityType.Posting, query, options.Value.CandidateRetrievalLimit, ct))
        {
            var known = await knownPostings.GetAsync(hit.EntityId, ct);
            if (known is not { IsActive: true })
            {
                continue;
            }

            var loaded = await LoadPostingAsync(hit.EntityId, refreshSemantics: false, ct);
            if (loaded is { View.IsActive: true } && (await ScoreAsync(profile, loaded.Value.View, config, ct)).Stored is not null)
            {
                count++;
            }
        }

        return count;
    }
}
