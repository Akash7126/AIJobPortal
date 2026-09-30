using JobPlatform.CandidateSourcing.Application.Commands.TalentPool;
using JobPlatform.CandidateSourcing.Domain.Interfaces.Repositories;
using JobPlatform.SharedKernel.Application.Interfaces.Cqrs;
using JobPlatform.SharedKernel.Application.Interfaces.Ports;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.CandidateSourcing.Application.Handlers.TalentPool;

internal sealed class RemoveFromTalentPoolHandler : ICommandHandler<RemoveFromTalentPoolCommand, Unit>
{
    private readonly ITalentPoolEntryRepository _entries;
    private readonly ICurrentUser _user;

    public RemoveFromTalentPoolHandler(ITalentPoolEntryRepository entries, ICurrentUser user)
    {
        _entries = entries;
        _user = user;
    }

    public async Task<Result<Unit>> Handle(RemoveFromTalentPoolCommand request, CancellationToken ct)
    {
        var entry = await _entries.GetByIdAsync(request.Id, ct);
        if (entry is null)
        {
            return Error.NotFound(Domain.Common.ErrorCodes.NotFound, "The talent-pool entry was not found.");
        }

        entry.Remove(ActorFactory.From(_user).Id);
        return Result.Success();
    }
}
