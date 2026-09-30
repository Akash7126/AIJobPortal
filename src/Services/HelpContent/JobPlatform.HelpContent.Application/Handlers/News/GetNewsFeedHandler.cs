using JobPlatform.HelpContent.Application.DTOs.News;
using JobPlatform.HelpContent.Application.Interfaces;
using JobPlatform.HelpContent.Application.Queries.News;
using JobPlatform.SharedKernel.Application.Interfaces.Cqrs;
using JobPlatform.SharedKernel.Application.Interfaces.Ports;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.HelpContent.Application.Handlers.News;

internal sealed class GetNewsFeedHandler : IQueryHandler<GetNewsFeedQuery, IReadOnlyList<NewsListItemView>>
{
    private const int FeedSize = 20;

    private readonly IHelpContentReadStore _store;
    private readonly IProfileInterestsProvider _interests;
    private readonly ICurrentUser _user;

    public GetNewsFeedHandler(IHelpContentReadStore store, IProfileInterestsProvider interests, ICurrentUser user)
    {
        _store = store;
        _interests = interests;
        _user = user;
    }

    public async Task<Result<IReadOnlyList<NewsListItemView>>> Handle(GetNewsFeedQuery request, CancellationToken ct)
    {
        var tags = _user.UserId is { } id ? await _interests.GetInterestTagsAsync(id, ct) : Array.Empty<string>();
        if (tags.Count == 0)
        {
            return Result.Success(await _store.GetGeneralFeedAsync(FeedSize, ct));
        }

        var personalized = await _store.GetPersonalizedFeedAsync(tags, FeedSize, ct);
        return Result.Success(personalized.Count > 0 ? personalized : await _store.GetGeneralFeedAsync(FeedSize, ct));
    }
}
