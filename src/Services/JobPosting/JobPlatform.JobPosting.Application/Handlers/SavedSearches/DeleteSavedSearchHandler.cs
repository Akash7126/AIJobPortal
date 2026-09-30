using JobPlatform.JobPosting.Application.Commands.SavedSearches;
using JobPlatform.JobPosting.Domain;
using JobPlatform.JobPosting.Domain.Interfaces.Repositories;
using JobPlatform.SharedKernel.Application.Interfaces.Cqrs;
using JobPlatform.SharedKernel.Application.Interfaces.Ports;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.JobPosting.Application.Handlers.SavedSearches;

internal sealed class DeleteSavedSearchHandler : ICommandHandler<DeleteSavedSearchCommand, Unit>
{
    private readonly ISavedSearchRepository _searches;
    private readonly ICurrentUser _user;

    public DeleteSavedSearchHandler(ISavedSearchRepository searches, ICurrentUser user)
    {
        _searches = searches;
        _user = user;
    }

    public async Task<Result<Unit>> Handle(DeleteSavedSearchCommand request, CancellationToken ct)
    {
        var search = await _searches.GetByIdAsync(request.SavedSearchId, ct);
        if (search is null)
        {
            return Result.Success();
        }

        if (search.OwnerAccountId != _user.UserId)
        {
            return Error.Forbidden(ErrorCodes.FavoriteForbidden, "Only the owner may delete this saved search.");
        }

        _searches.Remove(search);
        return Result.Success();
    }
}
