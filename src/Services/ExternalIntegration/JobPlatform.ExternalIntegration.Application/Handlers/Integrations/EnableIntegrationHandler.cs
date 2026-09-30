using JobPlatform.ExternalIntegration.Application.Commands.Integrations;
using JobPlatform.ExternalIntegration.Domain;
using JobPlatform.ExternalIntegration.Domain.Interfaces.Repositories;
using JobPlatform.SharedKernel.Application.Interfaces.Cqrs;
using JobPlatform.SharedKernel.Application.Interfaces.Ports;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.ExternalIntegration.Application.Handlers.Integrations;

internal sealed class EnableIntegrationHandler : ICommandHandler<EnableIntegrationCommand, Unit>
{
    private readonly IExternalJobSiteIntegrationRepository _integrations;
    private readonly ICurrentUser _user;
    private readonly TimeProvider _clock;

    public EnableIntegrationHandler(IExternalJobSiteIntegrationRepository integrations, ICurrentUser user, TimeProvider clock)
    {
        _integrations = integrations;
        _user = user;
        _clock = clock;
    }

    public async Task<Result<Unit>> Handle(EnableIntegrationCommand request, CancellationToken ct)
    {
        var integration = await _integrations.GetByPartnerAccountAsync(_user.UserId!.Value, ct);
        if (integration is null)
        {
            return Error.NotFound(ErrorCodes.NotFound, "No integration was found for this partner account.");
        }

        integration.Enable(request.PullEnabled, request.PushEnabled, ActorFactory.From(_user), _clock.GetUtcNow().UtcDateTime);
        return Result.Success();
    }
}
