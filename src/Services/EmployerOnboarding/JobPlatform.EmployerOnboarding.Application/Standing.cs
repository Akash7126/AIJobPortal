using JobPlatform.EmployerOnboarding.Application.Interfaces;
using JobPlatform.EmployerOnboarding.Domain;
using JobPlatform.EmployerOnboarding.Domain.Interfaces.Repositories;
using JobPlatform.SharedKernel.IntegrationEvents.GovernmentIntegration;
using JobPlatform.SharedKernel.Messaging.Interfaces;

namespace JobPlatform.EmployerOnboarding.Application;

/// <summary>Sets the "Verified Employer" badge flag once BC-01 approves the employer's government verification. Idempotent and monotonic:
/// a duplicate delivery or an older/out-of-order event (including one that arrives before the registration exists) never regresses the flag.</summary>
public sealed class MarkEmployerVerifiedHandler : IIntegrationEventHandler<EmployerVerificationApprovedIntegrationEvent>
{
    private readonly IEmployerStandingRepository _standings;
    private readonly IEmployerCache _cache;
    private readonly TimeProvider _clock;

    public MarkEmployerVerifiedHandler(IEmployerStandingRepository standings, IEmployerCache cache, TimeProvider clock)
    {
        _standings = standings;
        _cache = cache;
        _clock = clock;
    }

    public async Task Handle(EmployerVerificationApprovedIntegrationEvent integrationEvent, CancellationToken ct)
    {
        var standing = await _standings.GetAsync(integrationEvent.EmployerAccountId, ct);
        if (standing is null)
        {
            // The registration/standing has not been opened yet (AccountApproved has not been processed, or never will be for this BC).
            // Open one so the badge is not lost; EmployerRegistration itself is opened independently by OpenEmployerRegistrationHandler.
            standing = EmployerStanding.OpenFor(Guid.NewGuid(), integrationEvent.EmployerAccountId);
            _standings.Add(standing);
        }

        standing.MarkVerified(integrationEvent.MessageId, integrationEvent.AggregateVersion, _clock.GetUtcNow().UtcDateTime);
        await _cache.InvalidateAsync(integrationEvent.EmployerAccountId, ct);
    }
}
