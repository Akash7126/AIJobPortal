using JobPlatform.HelpContent.Domain;
using JobPlatform.SharedKernel.Application.Abstractions;
using JobPlatform.SharedKernel.Application.Ports;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.HelpContent.Application;

/// <summary>US-3.7.2-07: first access with no progress row means the tutorial is offered; completed means it is not auto-shown again but
/// remains reachable on request (AC-01/02). The tutorial's own content is a HelpContent(Kind=Guide), read like any other help article.</summary>
public sealed record GetOnboardingTutorialQuery(Guid TutorialId) : AuthenticatedQuery<TutorialView>;

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

public sealed record CompleteTutorialCommand(Guid TutorialId) : AuthenticatedCommand<Unit>;

internal sealed class CompleteTutorialHandler : ICommandHandler<CompleteTutorialCommand, Unit>
{
    private readonly ITutorialProgressRepository _progress;
    private readonly ICurrentUser _user;
    private readonly TimeProvider _clock;

    public CompleteTutorialHandler(ITutorialProgressRepository progress, ICurrentUser user, TimeProvider clock)
    {
        _progress = progress;
        _user = user;
        _clock = clock;
    }

    public async Task<Result<Unit>> Handle(CompleteTutorialCommand request, CancellationToken ct)
    {
        var userId = _user.UserId!.Value;
        if (await _progress.GetAsync(userId, request.TutorialId, ct) is not null)
        {
            return Result.Success();
        }

        _progress.Add(TutorialProgress.Complete(Guid.NewGuid(), userId, request.TutorialId, _clock.GetUtcNow().UtcDateTime));
        return Result.Success();
    }
}
