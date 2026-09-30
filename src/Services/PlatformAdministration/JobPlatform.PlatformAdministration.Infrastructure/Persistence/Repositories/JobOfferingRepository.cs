using JobPlatform.PlatformAdministration.Domain.Interfaces.Repositories;
using JobPlatform.PlatformAdministration.Domain.Offerings;
using Microsoft.EntityFrameworkCore;

namespace JobPlatform.PlatformAdministration.Infrastructure.Persistence.Repositories;

internal sealed class JobOfferingRepository(AdminDbContext db) : IJobOfferingRepository
{
    public Task<JobOffering?> GetByIdAsync(Guid jobPostingId, CancellationToken ct = default) =>
        db.JobOfferings.FirstOrDefaultAsync(o => o.Id == jobPostingId, ct);

    public Task<bool> ExistsAsync(Guid jobPostingId, CancellationToken ct = default) => db.JobOfferings.AnyAsync(o => o.Id == jobPostingId, ct);

    public void Add(JobOffering offering) => db.JobOfferings.Add(offering);
}
