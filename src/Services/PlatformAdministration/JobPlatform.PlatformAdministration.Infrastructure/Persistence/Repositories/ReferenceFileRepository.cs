using JobPlatform.PlatformAdministration.Domain.Interfaces.Repositories;
using JobPlatform.PlatformAdministration.Domain.Reference;
using Microsoft.EntityFrameworkCore;

namespace JobPlatform.PlatformAdministration.Infrastructure.Persistence.Repositories;

internal sealed class ReferenceFileRepository(AdminDbContext db) : IReferenceFileRepository
{
    public Task<ReferenceFile?> GetByTypeAsync(ReferenceFileType type, CancellationToken ct = default) =>
        db.ReferenceFiles.Include(f => f.Entries).FirstOrDefaultAsync(f => f.Type == type, ct);

    public void Add(ReferenceFile file) => db.ReferenceFiles.Add(file);
}
