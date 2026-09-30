using JobPlatform.Notification.Domain;
using JobPlatform.Notification.Domain.Interfaces.Repositories;
using Microsoft.EntityFrameworkCore;

namespace JobPlatform.Notification.Infrastructure.Persistence.Repositories;

internal sealed class SmsPolicyRepository : ISmsPolicyRepository
{
    private readonly NotificationDbContext _db;

    public SmsPolicyRepository(NotificationDbContext db) => _db = db;

    public async Task<SmsPolicy> GetCurrentAsync(CancellationToken ct = default) =>
        await _db.SmsPolicies.OrderByDescending(p => p.Id).FirstOrDefaultAsync(ct) ?? SmsPolicy.Initial(DateTime.UtcNow);

    public void Add(SmsPolicy policy) => _db.SmsPolicies.Add(policy);
}
