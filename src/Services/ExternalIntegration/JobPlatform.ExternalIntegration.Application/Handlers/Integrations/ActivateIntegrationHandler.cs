using JobPlatform.ExternalIntegration.Application.Commands.Integrations;
using JobPlatform.ExternalIntegration.Domain;
using JobPlatform.ExternalIntegration.Domain.Interfaces.Repositories;
using JobPlatform.SharedKernel.Application.Interfaces.Cqrs;
using JobPlatform.SharedKernel.Application.Interfaces.Ports;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.ExternalIntegration.Application.Handlers.Integrations;

internal sealed class ActivateIntegrationHandler : ICommandHandler<ActivateIntegrationCommand, Unit>
{
    private readonly IExternalJobSiteIntegrationRepository _integrations;
    private readonly ICurrentUser _user;

    public ActivateIntegrationHandler(IExternalJobSiteIntegrationRepository integrations, ICurrentUser user)
    {
        _integrations = integrations;
        _user = user;
    }

    public async Task<Result<Unit>> Handle(ActivateIntegrationCommand request, CancellationToken ct)
    {
        var integration = await _integrations.GetByIdAsync(request.IntegrationId, ct);
        if (integration is null)
        {
            return Error.NotFound(ErrorCodes.NotFound, "The integration was not found.");
        }

        integration.Activate(ActorFactory.From(_user));
        return Result.Success();
    }
}
