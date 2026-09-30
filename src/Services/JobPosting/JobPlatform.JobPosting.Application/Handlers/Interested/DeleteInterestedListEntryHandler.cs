using JobPlatform.JobPosting.Application.Commands.Interested;
using JobPlatform.JobPosting.Domain;
using JobPlatform.JobPosting.Domain.Interfaces.Repositories;
using JobPlatform.SharedKernel.Application.Interfaces.Cqrs;
using JobPlatform.SharedKernel.Application.Interfaces.Ports;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.JobPosting.Application.Handlers.Interested;

internal sealed class DeleteInterestedListEntryHandler : ICommandHandler<DeleteInterestedListEntryCommand, Unit>
{
    private readonly IInterestedListRepository _entries;
    private readonly ICurrentUser _user;

    public DeleteInterestedListEntryHandler(IInterestedListRepository entries, ICurrentUser user)
    {
        _entries = entries;
        _user = user;
    }

    public async Task<Result<Unit>> Handle(DeleteInterestedListEntryCommand request, CancellationToken ct)
    {
        var entry = await _entries.GetByIdAsync(request.InterestedListEntryId, ct);
        if (entry is null)
        {
            return Result.Success();
        }

        if (entry.OwnerAccountId != _user.UserId)
        {
            return Error.Forbidden(ErrorCodes.InterestedForbidden, "Only the owner may delete this entry.");
        }

        _entries.Remove(entry);
        return Result.Success();
    }
}
