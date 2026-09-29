using JobPlatform.ExternalIntegration.Application.Commands.Integrations;
using JobPlatform.ExternalIntegration.Domain;
using JobPlatform.SharedKernel.Application.Abstractions;
using JobPlatform.SharedKernel.Application.Ports;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.ExternalIntegration.Application.Handlers.Integrations;

internal sealed class ApproveExternalJobSiteHandler : ICommandHandler<ApproveExternalJobSiteCommand, Unit>
{
    private readonly IExternalJobSiteIntegrationRepository _integrations;
    private readonly ICurrentUser _user;
    private readonly TimeProvider _clock;

    public ApproveExternalJobSiteHandler(IExternalJobSiteIntegrationRepository integrations, ICurrentUser user, TimeProvider clock)
    {
        _integrations = integrations;
        _user = user;
        _clock = clock;
    }

    public async Task<Result<Unit>> Handle(ApproveExternalJobSiteCommand request, CancellationToken ct)
    {
        var integration = await _integrations.GetByIdAsync(request.IntegrationId, ct);
        if (integration is null)
        {
            return Error.NotFound(ErrorCodes.NotFound, "The integration was not found.");
        }

        integration.ApproveByMolPef(ActorFactory.From(_user), request.ApprovalBasis, _clock.GetUtcNow().UtcDateTime);
        return Result.Success();
    }
}
