using JobPlatform.AccountIdentity.Domain.Consent;
using JobPlatform.AccountIdentity.Domain.Interfaces.Repositories;
using Microsoft.EntityFrameworkCore;

namespace JobPlatform.AccountIdentity.Infrastructure.Persistence.Repositories;

internal sealed class PrivacyConsentRepository : IPrivacyConsentRepository
{
    private readonly IdentityDbContext _db;

    public PrivacyConsentRepository(IdentityDbContext db) => _db = db;

    public Task<PrivacyConsent?> GetAsync(Guid guestId, string policyVersion, CancellationToken ct = default) =>
        _db.PrivacyConsents.FirstOrDefaultAsync(c => c.GuestId == guestId && c.PolicyVersion == policyVersion, ct);

    public void Add(PrivacyConsent consent) => _db.PrivacyConsents.Add(consent);
}
