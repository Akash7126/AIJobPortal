using JobPlatform.AccountIdentity.Domain.Consent;

namespace JobPlatform.AccountIdentity.Domain.Interfaces.Repositories;

public interface IPrivacyConsentRepository
{
    Task<PrivacyConsent?> GetAsync(Guid guestId, string policyVersion, CancellationToken ct = default);

    void Add(PrivacyConsent consent);
}
