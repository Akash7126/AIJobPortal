using JobPlatform.JobPosting.Application.Commands.Favorites;
using JobPlatform.JobPosting.Domain;
using JobPlatform.SharedKernel.Application.Abstractions;
using JobPlatform.SharedKernel.Application.Ports;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.JobPosting.Application.Handlers.Favorites;

internal sealed class ToggleFavoriteJobHandler : ICommandHandler<ToggleFavoriteJobCommand, bool>
{
    private readonly IFavoriteJobListRepository _favorites;
    private readonly IJobPostingRepository _postings;
    private readonly ICurrentUser _user;
    private readonly TimeProvider _clock;

    public ToggleFavoriteJobHandler(IFavoriteJobListRepository favorites, IJobPostingRepository postings, ICurrentUser user, TimeProvider clock)
    {
        _favorites = favorites;
        _postings = postings;
        _user = user;
        _clock = clock;
    }

    public async Task<Result<bool>> Handle(ToggleFavoriteJobCommand request, CancellationToken ct)
    {
        var owner = _user.UserId!.Value;
        var posting = await _postings.GetByIdAsync(request.JobPostingId, ct);
        if (posting is null || posting.Status == JobPostingStatus.Archived)
        {
            return Error.NotFound(ErrorCodes.NotFound, "The job posting was not found.");
        }

        var list = await _favorites.GetByOwnerAsync(owner, ct);
        if (list is null)
        {
            list = FavoriteJobList.CreateEmpty(owner);
            _favorites.Add(list);
        }

        return list.Toggle(request.JobPostingId, ActorFactory.From(_user), _clock.GetUtcNow().UtcDateTime);
    }
}
