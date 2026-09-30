using JobPlatform.ExternalIntegration.Application.Commands.Integrations;
using JobPlatform.ExternalIntegration.Domain;
using JobPlatform.ExternalIntegration.Domain.Interfaces.Repositories;
using JobPlatform.SharedKernel.Application.Interfaces.Cqrs;
using JobPlatform.SharedKernel.Application.Interfaces.Ports;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.ExternalIntegration.Application.Handlers.Integrations;

internal sealed class ConfigureSyncScheduleHandler : ICommandHandler<ConfigureSyncScheduleCommand, Unit>
{
    private readonly IExternalJobSiteIntegrationRepository _integrations;
    private readonly ICurrentUser _user;

    public ConfigureSyncScheduleHandler(IExternalJobSiteIntegrationRepository integrations, ICurrentUser user)
    {
        _integrations = integrations;
        _user = user;
    }

    public async Task<Result<Unit>> Handle(ConfigureSyncScheduleCommand request, CancellationToken ct)
    {
        var integration = await _integrations.GetByPartnerAccountAsync(_user.UserId!.Value, ct);
        if (integration is null)
        {
            return Error.NotFound(ErrorCodes.NotFound, "No integration was found for this partner account.");
        }

        integration.ConfigureSyncSchedule(Enum.Parse<SyncMode>(request.Mode, true), request.Cron, ActorFactory.From(_user));
        return Result.Success();
    }
}
