using JobPlatform.HelpContent.Application.Commands.Tutorials;
using JobPlatform.HelpContent.Domain;
using JobPlatform.HelpContent.Domain.Interfaces.Repositories;
using JobPlatform.SharedKernel.Application.Interfaces.Cqrs;
using JobPlatform.SharedKernel.Application.Interfaces.Ports;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.HelpContent.Application.Handlers.Tutorials;

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
