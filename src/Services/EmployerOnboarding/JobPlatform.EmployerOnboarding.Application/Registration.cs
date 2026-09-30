using JobPlatform.EmployerOnboarding.Domain;
using JobPlatform.EmployerOnboarding.Domain.Interfaces.Repositories;
using JobPlatform.SharedKernel.Common.Enums;
using JobPlatform.SharedKernel.IntegrationEvents.AccountIdentity;
using JobPlatform.SharedKernel.Messaging.Interfaces;

namespace JobPlatform.EmployerOnboarding.Application;

// ---------------------------------------------------------------------- inbox

/// <summary>Opens a Pending registration and a standing row when BC-03 approves an employer account. Idempotent per employer account
/// (a redelivery, or a second AccountApproved for the same account, changes nothing).</summary>
public sealed class OpenEmployerRegistrationHandler : IIntegrationEventHandler<AccountApprovedIntegrationEvent>
{
    private readonly IEmployerRegistrationRepository _registrations;
    private readonly IEmployerStandingRepository _standings;
    private readonly IKnownAccountRepository _knownAccounts;
    private readonly TimeProvider _clock;

    public OpenEmployerRegistrationHandler(IEmployerRegistrationRepository registrations, IEmployerStandingRepository standings,
        IKnownAccountRepository knownAccounts, TimeProvider clock)
    {
        _registrations = registrations;
        _standings = standings;
        _knownAccounts = knownAccounts;
        _clock = clock;
    }

    public async Task Handle(AccountApprovedIntegrationEvent integrationEvent, CancellationToken ct)
    {
        if (integrationEvent.ActorType != ActorType.Employer)
        {
            return;
        }

        var now = _clock.GetUtcNow().UtcDateTime;
        if (await _knownAccounts.GetAsync(integrationEvent.AccountId, ct) is null)
        {
            _knownAccounts.Add(new KnownAccount(integrationEvent.AccountId, now));
        }

        if (await _registrations.GetByEmployerAsync(integrationEvent.AccountId, ct) is null)
        {
            _registrations.Add(EmployerRegistration.OpenFor(Guid.NewGuid(), integrationEvent.AccountId, now));
        }

        if (await _standings.GetAsync(integrationEvent.AccountId, ct) is null)
        {
            _standings.Add(EmployerStanding.OpenFor(Guid.NewGuid(), integrationEvent.AccountId));
        }
    }
}
