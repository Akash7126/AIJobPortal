using JobPlatform.ExternalIntegration.Application.Commands.Integrations;
using JobPlatform.ExternalIntegration.Application.DTOs.Integrations;
using JobPlatform.ExternalIntegration.Domain;
using JobPlatform.SharedKernel.Application.Abstractions;
using JobPlatform.SharedKernel.Application.Ports;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.ExternalIntegration.Application.Handlers.Integrations;

internal sealed class RegisterExternalJobSiteHandler : ICommandHandler<RegisterExternalJobSiteCommand, IntegrationView>
{
    private readonly IExternalJobSiteIntegrationRepository _integrations;
    private readonly IKnownPartnerAccountRepository _knownAccounts;
    private readonly ICurrentUser _user;
    private readonly TimeProvider _clock;

    public RegisterExternalJobSiteHandler(IExternalJobSiteIntegrationRepository integrations, IKnownPartnerAccountRepository knownAccounts,
        ICurrentUser user, TimeProvider clock)
    {
        _integrations = integrations;
        _knownAccounts = knownAccounts;
        _user = user;
        _clock = clock;
    }

    public async Task<Result<IntegrationView>> Handle(RegisterExternalJobSiteCommand request, CancellationToken ct)
    {
        var partnerAccountId = _user.UserId!.Value;
        if (await _knownAccounts.GetAsync(partnerAccountId, ct) is null)
        {
            return Error.Forbidden(ErrorCodes.PartnerForbidden, "This account is not yet a recognised, active external job site.");
        }

        if (await _integrations.GetByPartnerAccountAsync(partnerAccountId, ct) is not null)
        {
            return Error.Conflict(ErrorCodes.AlreadyRegistered, "This partner account already has an integration registered.");
        }

        var platform = new SourcePlatform(Guid.NewGuid(), request.SourcePlatformName, request.BaseUrl);
        var integration = ExternalJobSiteIntegration.Register(Guid.NewGuid(), partnerAccountId, platform, request.RecommendedByMolPef,
            _clock.GetUtcNow().UtcDateTime);
        _integrations.Add(integration);
        return Views.ToView(integration);
    }
}
