using JobPlatform.AiMatching.Application.Commands.Semantics;
using JobPlatform.AiMatching.Application.Interfaces;
using JobPlatform.AiMatching.Domain.Interfaces.Repositories;
using JobPlatform.SharedKernel.Application.Interfaces.Cqrs;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.AiMatching.Application.Handlers.Semantics;

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
