using JobPlatform.ExternalIntegration.Application.Commands.JobDataFlows;
using JobPlatform.ExternalIntegration.Domain;
using JobPlatform.ExternalIntegration.Domain.Interfaces.Repositories;
using JobPlatform.SharedKernel.Application.Interfaces.Cqrs;
using JobPlatform.SharedKernel.Application.Interfaces.Ports;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.ExternalIntegration.Application.Handlers.JobDataFlows;

internal sealed class SyncJobPostAttributionHandler : ICommandHandler<SyncJobPostAttributionCommand, Unit>
{
    private readonly IJobPostAttributionRepository _attributions;
    private readonly ICurrentUser _user;
    private readonly TimeProvider _clock;

    public SyncJobPostAttributionHandler(IJobPostAttributionRepository attributions, ICurrentUser user, TimeProvider clock)
    {
        _attributions = attributions;
        _user = user;
        _clock = clock;
    }

    public async Task<Result<Unit>> Handle(SyncJobPostAttributionCommand request, CancellationToken ct)
    {
        var attribution = await _attributions.GetByPlatformJobIdAsync(request.PlatformJobId, ct);
        if (attribution is null)
        {
            return Error.NotFound(ErrorCodes.NotFound, "No attribution was found for this platform job id.");
        }

        var actor = ActorFactory.From(_user);
        var now = _clock.GetUtcNow().UtcDateTime;
        switch (Enum.Parse<AttributionOperation>(request.Operation, true))
        {
            case AttributionOperation.ExtendDeadline:
                attribution.ExtendDeadline(request.Deadline!.Value, actor, now);
                break;
            case AttributionOperation.EditDescription:
                attribution.EditDescription(request.Description!, actor, now);
                break;
            case AttributionOperation.Close:
                attribution.Close(actor, now);
                break;
            case AttributionOperation.Deactivate:
                attribution.Deactivate(actor, now);
                break;
            case AttributionOperation.Delete:
                attribution.Delete(actor, now);
                break;
        }

        return Result.Success();
    }
}
