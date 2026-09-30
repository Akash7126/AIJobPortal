using JobPlatform.AiMatching.Application.Commands.Matching;
using JobPlatform.AiMatching.Application.Commands.Parsing;
using JobPlatform.AiMatching.Application.Commands.Recommendations;
using JobPlatform.AiMatching.Application.Commands.Semantics;
using JobPlatform.AiMatching.Application.Commands.Shortlists;
using JobPlatform.AiMatching.Domain;
using JobPlatform.AiMatching.Domain.Interfaces.Repositories;
using JobPlatform.SharedKernel.Application.Interfaces.Cqrs;
using JobPlatform.SharedKernel.Application.Interfaces.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace JobPlatform.AiMatching.Application;

/// <summary>
/// Executes queued work (batch shortlist, fan-out matching, embedding refresh, re-standardisation, recommendations) outside the request and the inbox transaction
/// (handover 3.9). Each item runs through the normal command pipeline in its own scope, so its aggregate changes and outbox rows commit together; the item's own
/// state is then recorded in a second scope so a failing command never loses the retry bookkeeping.
/// </summary>
public sealed class MatchingWorkRunner(IServiceScopeFactory scopes, IOptions<MatchingOptions> options, TimeProvider clock, ILogger<MatchingWorkRunner> logger)
{
    /// <summary>Runs one batch of due items; returns how many were attempted.</summary>
    public async Task<int> RunOnceAsync(CancellationToken ct)
    {
        IReadOnlyList<(Guid Id, WorkItemKind Kind, Guid EntityId)> due;
        using (var scope = scopes.CreateScope())
        {
            var items = await scope.ServiceProvider.GetRequiredService<IWorkItemRepository>().ListDueAsync(clock.GetUtcNow().UtcDateTime, options.Value.WorkBatchSize, ct);
            due = items.Select(i => (i.Id, i.Kind, i.EntityId)).ToArray();
        }

        foreach (var (id, kind, entityId) in due)
        {
            string? error = null;
            try
            {
                using var scope = scopes.CreateScope();
                var result = await Dispatch(scope.ServiceProvider.GetRequiredService<ISender>(), kind, entityId, ct);
                error = result;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                error = ex.Message;
                logger.LogWarning(ex, "Work item {WorkItemId} ({Kind}) failed", id, kind);
            }

            await RecordAsync(id, error, ct);
        }

        return due.Count;
    }

    private static async Task<string?> Dispatch(ISender sender, WorkItemKind kind, Guid entityId, CancellationToken ct)
    {
        switch (kind)
        {
            case WorkItemKind.RefreshProfileEmbedding:
                var command = new RefreshProfileEmbeddingCommand(entityId);
                return Failure(await sender.Send(command, ct));
            case WorkItemKind.MatchesForPosting:
                var computeMatchesForPostingCommand = new ComputeMatchesForPostingCommand(entityId);
                return Failure(await sender.Send(computeMatchesForPostingCommand, ct));
            case WorkItemKind.MatchesForProfile:
                var computeMatchesForProfileCommand = new ComputeMatchesForProfileCommand(entityId);
                return Failure(await sender.Send(computeMatchesForProfileCommand, ct));
            case WorkItemKind.ComputeShortlist:
                var runCandidateShortlistCommand = new RunCandidateShortlistCommand(entityId);
                return Failure(await sender.Send(runCandidateShortlistCommand, ct));
            case WorkItemKind.ComputeRecommendation:
                var computeJobRecommendationCommand = new ComputeJobRecommendationCommand(entityId, Actor.SystemId);
                return Failure(await sender.Send(computeJobRecommendationCommand, ct));
            case WorkItemKind.Restandardize:
                int changed;
                do
                {
                    var standardizeSkillsCommand = new StandardizeSkillsCommand(null, 100);
                    var result = await sender.Send(standardizeSkillsCommand, ct);
                    if (result.IsFailure)
                    {
                        return result.Error!.Code;
                    }

                    changed = result.Value;
                }
                while (changed >= 100);
                return null;
            default:
                return $"Unknown work item kind {kind}";
        }
    }

    private static string? Failure(JobPlatform.SharedKernel.Application.Results.Result result) => result.IsFailure ? result.Error!.Code : null;

    private async Task RecordAsync(Guid id, string? error, CancellationToken ct)
    {
        using var scope = scopes.CreateScope();
        var item = await scope.ServiceProvider.GetRequiredService<IWorkItemRepository>().GetAsync(id, ct);
        if (item is null)
        {
            return;
        }

        var now = clock.GetUtcNow().UtcDateTime;
        if (error is null)
        {
            item.MarkDone(now);
        }
        else
        {
            item.MarkFailed(error, now, options.Value.WorkMaxAttempts, TimeSpan.FromSeconds(Math.Min(300, 5 * Math.Pow(2, item.Attempts))));
        }

        await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(ct);
    }
}
