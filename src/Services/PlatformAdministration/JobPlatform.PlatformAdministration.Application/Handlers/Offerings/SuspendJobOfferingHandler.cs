using JobPlatform.PlatformAdministration.Application.Commands.Offerings;
using JobPlatform.PlatformAdministration.Domain;
using JobPlatform.PlatformAdministration.Domain.Common;
using JobPlatform.SharedKernel.Application.Abstractions;
using JobPlatform.SharedKernel.Application.Ports;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.PlatformAdministration.Application.Handlers.Offerings;

internal sealed class SuspendJobOfferingHandler : ICommandHandler<SuspendJobOfferingCommand, Guid>
{
    private readonly IJobOfferingRepository _offerings;
    private readonly ICurrentUser _user;
    private readonly TimeProvider _clock;

    public SuspendJobOfferingHandler(IJobOfferingRepository offerings, ICurrentUser user, TimeProvider clock)
    {
        _offerings = offerings;
        _user = user;
        _clock = clock;
    }

    public async Task<Result<Guid>> Handle(SuspendJobOfferingCommand request, CancellationToken ct)
    {
        var offering = await _offerings.GetByIdAsync(request.JobOfferingId, ct);
        if (offering is null)
        {
            return Error.NotFound(ErrorCodes.NotFound, "The job offering was not found.");
        }

        offering.Suspend(ActorFactory.From(_user), request.Reason, _clock.GetUtcNow().UtcDateTime);
        return offering.Id;
    }
}
