using JobPlatform.AiMatching.Application.Commands.Matching;
using JobPlatform.AiMatching.Application.Services.Matching;
using JobPlatform.SharedKernel.Application.Interfaces.Cqrs;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.AiMatching.Application.Handlers.Matching;

internal sealed class ComputeMatchesForPostingHandler(MatchComputationService computation) : ICommandHandler<ComputeMatchesForPostingCommand, int>
{
    public async Task<Result<int>> Handle(ComputeMatchesForPostingCommand request, CancellationToken ct) => await computation.ComputeForPostingAsync(request.JobPostingId, ct);
}
