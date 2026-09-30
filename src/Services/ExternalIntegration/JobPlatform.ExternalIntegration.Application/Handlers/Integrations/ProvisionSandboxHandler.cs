using JobPlatform.ExternalIntegration.Application.Commands.Integrations;
using JobPlatform.ExternalIntegration.Domain;
using JobPlatform.ExternalIntegration.Domain.Interfaces.Repositories;
using JobPlatform.SharedKernel.Application.Interfaces.Cqrs;
using JobPlatform.SharedKernel.Application.Interfaces.Ports;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.ExternalIntegration.Application.Handlers.Integrations;

internal sealed class ProvisionSandboxHandler : ICommandHandler<ProvisionSandboxCommand, Unit>
{
    private readonly IExternalJobSiteIntegrationRepository _integrations;
    private readonly IPartnerCredentialRepository _credentials;
    private readonly ICurrentUser _user;
    private readonly TimeProvider _clock;

    public ProvisionSandboxHandler(IExternalJobSiteIntegrationRepository integrations, IPartnerCredentialRepository credentials, ICurrentUser user,
        TimeProvider clock)
    {
        _integrations = integrations;
        _credentials = credentials;
        _user = user;
        _clock = clock;
    }

    public async Task<Result<Unit>> Handle(ProvisionSandboxCommand request, CancellationToken ct)
    {
        var partnerAccountId = _user.UserId!.Value;
        var integration = await _integrations.GetByPartnerAccountAsync(partnerAccountId, ct);
        if (integration is null)
        {
            return Error.NotFound(ErrorCodes.NotFound, "No integration was found for this partner account.");
        }

        var now = _clock.GetUtcNow().UtcDateTime;
        var hasActiveCredential = await _credentials.HasActiveCredentialAsync(partnerAccountId, now, ct);
        integration.ProvisionSandbox(hasActiveCredential, ActorFactory.From(_user), now);
        return Result.Success();
    }
}
