using FluentValidation;
using JobPlatform.JobPosting.Domain;
using JobPlatform.SharedKernel.Application.Abstractions;
using JobPlatform.SharedKernel.Application.Ports;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.JobPosting.Application;

/// <summary>US-3.2.2-03: heart-button toggle. Returns whether the posting is now favourited.</summary>
public sealed record ToggleFavoriteJobCommand(Guid JobPostingId) : JobSeekerCommand<bool>;

public sealed record ListFavoritesQuery : JobSeekerQuery<FavoriteListView>;

public sealed class ToggleFavoriteJobValidator : AbstractValidator<ToggleFavoriteJobCommand>
{
    public ToggleFavoriteJobValidator() => RuleFor(c => c.JobPostingId).NotEmpty().WithErrorCode("VAL.JobPostingId.Required");
}

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

internal sealed class ListFavoritesHandler : IQueryHandler<ListFavoritesQuery, FavoriteListView>
{
    private readonly IFavoriteJobListRepository _favorites;
    private readonly ICurrentUser _user;

    public ListFavoritesHandler(IFavoriteJobListRepository favorites, ICurrentUser user)
    {
        _favorites = favorites;
        _user = user;
    }

    public async Task<Result<FavoriteListView>> Handle(ListFavoritesQuery request, CancellationToken ct)
    {
        var list = await _favorites.GetByOwnerAsync(_user.UserId!.Value, ct);
        return new FavoriteListView(list?.Id ?? Guid.Empty, list?.JobPostingIds ?? Array.Empty<Guid>());
    }
}
