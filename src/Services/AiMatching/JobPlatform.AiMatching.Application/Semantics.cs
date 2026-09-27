using JobPlatform.AiMatching.Domain;
using JobPlatform.SharedKernel.Application.Abstractions;
using JobPlatform.SharedKernel.Application.Results;
using Microsoft.Extensions.Logging;

namespace JobPlatform.AiMatching.Application;

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

// ---------------------------------------------------------------------- AnalyzeJobDescriptionCommand (inbox: JobPostingCreated)

/// <param name="Seed">What the event carried; BC-09's internal API is asked for the full posting and the seed is used when it is unavailable.</param>
public sealed record AnalyzeJobDescriptionCommand(PostingSource Seed) : ICommand;

internal sealed class AnalyzeJobDescriptionHandler(IPostingDirectory postings, PostingAnalysisService analysis, ILogger<AnalyzeJobDescriptionHandler> logger)
    : ICommandHandler<AnalyzeJobDescriptionCommand, Unit>
{
    public async Task<Result<Unit>> Handle(AnalyzeJobDescriptionCommand request, CancellationToken ct)
    {
        PostingSource source = request.Seed;
        try
        {
            source = await postings.GetAsync(request.Seed.JobPostingId, ct) ?? request.Seed;
        }
        catch (UpstreamUnavailableException ex)
        {
            logger.LogWarning(ex, "Posting {PostingId} could not be fetched; analysing the event data only", request.Seed.JobPostingId);
        }

        await analysis.AnalyzeAsync(source, ct);
        return Result.Success();
    }
}

// ---------------------------------------------------------------------- WithdrawPostingCommand (inbox: posting no longer active / suspended)

public sealed record WithdrawPostingCommand(Guid JobPostingId) : ICommand;

internal sealed class WithdrawPostingHandler(IMatchScoreRepository scores, IVectorIndex index) : ICommandHandler<WithdrawPostingCommand, Unit>
{
    /// <summary>De-indexes the posting and drops its stored scores so it can no longer be ranked or recommended.</summary>
    public async Task<Result<Unit>> Handle(WithdrawPostingCommand request, CancellationToken ct)
    {
        await scores.RemoveByPostingAsync(request.JobPostingId, ct);
        await index.RemoveAsync(VectorEntityType.Posting, request.JobPostingId, ct);
        return Result.Success();
    }
}

// ---------------------------------------------------------------------- RefreshProfileEmbeddingCommand (worker)

public sealed record RefreshProfileEmbeddingCommand(Guid ProfileId) : ICommand;

internal sealed class RefreshProfileEmbeddingHandler(IProfileDirectory profiles, IEmbeddingService embeddings, IVectorIndex index, IWorkItemRepository work,
    TimeProvider clock) : ICommandHandler<RefreshProfileEmbeddingCommand, Unit>
{
    public async Task<Result<Unit>> Handle(RefreshProfileEmbeddingCommand request, CancellationToken ct)
    {
        var text = await profiles.GetEmbeddingTextAsync(request.ProfileId, ct);
        if (text is null)
        {
            return Result.Success(); // the profile is gone: nothing to embed
        }

        await index.UpsertAsync(VectorEntityType.Profile, request.ProfileId, await embeddings.EmbedAsync(text, ct), embeddings.ModelVersion, ct);
        // Q-07: the profile changed, so its matches are recomputed against the active postings.
        await work.EnqueueAsync(WorkItemKind.MatchesForProfile, request.ProfileId, clock.GetUtcNow().UtcDateTime, ct);
        return Result.Success();
    }
}
