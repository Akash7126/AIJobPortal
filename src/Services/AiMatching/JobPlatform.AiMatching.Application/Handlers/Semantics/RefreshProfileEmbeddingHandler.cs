using JobPlatform.AiMatching.Application.Commands.Semantics;
using JobPlatform.AiMatching.Application.Interfaces;
using JobPlatform.AiMatching.Domain;
using JobPlatform.AiMatching.Domain.Interfaces.Repositories;
using JobPlatform.SharedKernel.Application.Interfaces.Cqrs;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.AiMatching.Application.Handlers.Semantics;

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
