using JobPlatform.Notification.Domain;
using JobPlatform.Notification.Domain.Interfaces.Repositories;
using Microsoft.EntityFrameworkCore;

namespace JobPlatform.Notification.Infrastructure.Persistence.Repositories;

internal sealed class JobConfirmationRepository : IJobConfirmationRepository
{
    private readonly NotificationDbContext _db;

    public JobConfirmationRepository(NotificationDbContext db) => _db = db;

    public Task<JobConfirmation?> GetAsync(Guid sourcePlatformId, string sourceJobId, CancellationToken ct = default) =>
        _db.JobConfirmations.FirstOrDefaultAsync(c => c.SourcePlatformId == sourcePlatformId && c.SourceJobId == sourceJobId, ct);

    public void Add(JobConfirmation confirmation) => _db.JobConfirmations.Add(confirmation);
}
