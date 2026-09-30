using JobPlatform.JobPosting.Application.Commands.SavedSearches;
using JobPlatform.JobPosting.Domain;
using JobPlatform.JobPosting.Domain.Interfaces.Repositories;
using JobPlatform.SharedKernel.Application.Interfaces.Cqrs;
using JobPlatform.SharedKernel.Application.Interfaces.Ports;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.JobPosting.Application.Handlers.SavedSearches;

internal sealed class UpdateSavedSearchHandler : ICommandHandler<UpdateSavedSearchCommand, Unit>
{
    private readonly ISavedSearchRepository _searches;
    private readonly ICurrentUser _user;

    public UpdateSavedSearchHandler(ISavedSearchRepository searches, ICurrentUser user)
    {
        _searches = searches;
        _user = user;
    }

    public async Task<Result<Unit>> Handle(UpdateSavedSearchCommand request, CancellationToken ct)
    {
        var search = await _searches.GetByIdAsync(request.SavedSearchId, ct);
        if (search is null)
        {
            return Error.NotFound(ErrorCodes.NotFound, "The saved search was not found.");
        }

        search.SetNotify(request.NotifyOnMatch, ActorFactory.From(_user));
        return Result.Success();
    }
}
