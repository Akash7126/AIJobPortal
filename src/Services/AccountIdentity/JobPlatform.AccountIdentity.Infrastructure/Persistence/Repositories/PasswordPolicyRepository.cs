using JobPlatform.AccountIdentity.Domain.Interfaces.Repositories;
using JobPlatform.AccountIdentity.Domain.PasswordPolicies;
using Microsoft.EntityFrameworkCore;

namespace JobPlatform.AccountIdentity.Infrastructure.Persistence.Repositories;

internal sealed class PasswordPolicyRepository : IPasswordPolicyRepository
{
    private readonly IdentityDbContext _db;
    private readonly TimeProvider _clock;

    public PasswordPolicyRepository(IdentityDbContext db, TimeProvider clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task<PasswordPolicy> GetAsync(CancellationToken ct = default)
    {
        var policy = await _db.PasswordPolicies.FirstOrDefaultAsync(p => p.Id == PasswordPolicy.SingletonId, ct);
        if (policy is not null)
        {
            return policy;
        }

        policy = PasswordPolicy.CreateDefault(_clock);
        _db.PasswordPolicies.Add(policy);
        return policy;
    }
}
