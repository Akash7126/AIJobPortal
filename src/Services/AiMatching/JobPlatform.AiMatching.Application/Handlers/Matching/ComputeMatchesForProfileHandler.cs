using JobPlatform.AiMatching.Application.Commands.Matching;
using JobPlatform.AiMatching.Application.Services.Matching;
using JobPlatform.SharedKernel.Application.Interfaces.Cqrs;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.AiMatching.Application.Handlers.Matching;

internal sealed class ComputeMatchesForProfileHandler(MatchComputationService computation) : ICommandHandler<ComputeMatchesForProfileCommand, int>
{
    public async Task<Result<int>> Handle(ComputeMatchesForProfileCommand request, CancellationToken ct) => await computation.ComputeForProfileAsync(request.ProfileId, ct);
}
