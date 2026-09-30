using JobPlatform.HelpContent.Application.DTOs.Tutorials;
using JobPlatform.HelpContent.Application.Interfaces;
using JobPlatform.HelpContent.Application.Queries.Tutorials;
using JobPlatform.HelpContent.Domain.Interfaces.Repositories;
using JobPlatform.SharedKernel.Application.Interfaces.Cqrs;
using JobPlatform.SharedKernel.Application.Interfaces.Ports;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.HelpContent.Application.Handlers.Tutorials;

internal sealed class GetOnboardingTutorialHandler : IQueryHandler<GetOnboardingTutorialQuery, TutorialView>
{
    private readonly IHelpContentReadStore _store;
    private readonly ITutorialProgressRepository _progress;
    private readonly ICurrentUser _user;

    public GetOnboardingTutorialHandler(IHelpContentReadStore store, ITutorialProgressRepository progress, ICurrentUser user)
    {
        _store = store;
        _progress = progress;
        _user = user;
    }

    public async Task<Result<TutorialView>> Handle(GetOnboardingTutorialQuery request, CancellationToken ct)
    {
        var content = await _store.GetHelpContentAsync(request.TutorialId, ct);
        if (content is null)
        {
            return Error.NotFound(Domain.Common.ErrorCodes.NotFound, "The tutorial was not found.");
        }

        var progress = await _progress.GetAsync(_user.UserId!.Value, request.TutorialId, ct);
        return new TutorialView(request.TutorialId, content.Title, content.Body, progress is not null, progress?.CompletedAtUtc);
    }
}
