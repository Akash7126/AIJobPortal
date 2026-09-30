using JobPlatform.GovernmentIntegration.Application.Commands.Verifications;
using JobPlatform.GovernmentIntegration.Domain.Interfaces.Repositories;
using JobPlatform.SharedKernel.Application.Interfaces.Cqrs;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.GovernmentIntegration.Application.Handlers.Verifications;

/// <summary>US-2.5-03 AC-03: retention job calls Purge() on GovernmentVerificationData past RetentionExpiresAtUtc (default 12 months, A-02-012),
/// clearing the encrypted VerifiedFields collection. Known limitation (documented in BC-01.md): EducationalCredentialVerification and
/// IdentityVerificationData carry a single small claim rather than a growable encrypted collection; their own retention purge is not yet wired
/// to a Purge() method, so <see cref="IEducationalCredentialVerificationRepository.ListExpiredAsync"/>/<see cref="IIdentityVerificationRepository.ListExpiredAsync"/>
/// exist for a future pass but are not invoked here.</summary>
internal sealed class PurgeExpiredGovernmentDataHandler : ICommandHandler<PurgeExpiredGovernmentDataCommand, int>
{
    private readonly IGovernmentVerificationDataRepository _govData;
    private readonly TimeProvider _clock;

    public PurgeExpiredGovernmentDataHandler(IGovernmentVerificationDataRepository govData, TimeProvider clock)
    {
        _govData = govData;
        _clock = clock;
    }

    public async Task<Result<int>> Handle(PurgeExpiredGovernmentDataCommand request, CancellationToken ct)
    {
        var now = _clock.GetUtcNow().UtcDateTime;
        var purged = 0;
        foreach (var record in await _govData.ListExpiredAsync(now, request.Take, ct))
        {
            record.Purge();
            purged++;
        }

        return purged;
    }
}
