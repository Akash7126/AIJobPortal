using JobPlatform.JobSeekerProfile.Domain;
using JobPlatform.JobSeekerProfile.Domain.Common;
using JobPlatform.JobSeekerProfile.Domain.Interfaces.Repositories;
using Microsoft.EntityFrameworkCore;

namespace JobPlatform.JobSeekerProfile.Infrastructure.Persistence.Repositories;

internal sealed class SupplementaryDocumentRepository(JobSeekerProfileDbContext db) : ISupplementaryDocumentRepository
{
    public async Task<SupplementaryDocument?> GetByHashAsync(DocumentOwnerType ownerType, Guid ownerId, string sha256, CancellationToken ct = default) =>
        (await db.Documents.Where(d => d.OwnerType == ownerType && d.OwnerId == ownerId).ToListAsync(ct))
        .FirstOrDefault(d => d.File.Sha256 == sha256);

    public Task<IReadOnlyList<SupplementaryDocument>> ListByOwnerAsync(DocumentOwnerType ownerType, Guid ownerId, CancellationToken ct = default) =>
        db.Documents.Where(d => d.OwnerType == ownerType && d.OwnerId == ownerId).ToListAsync(ct).ContinueWith(t => (IReadOnlyList<SupplementaryDocument>)t.Result, ct);

    public Task<SupplementaryDocument?> GetByIdAsync(Guid id, CancellationToken ct = default) => db.Documents.FirstOrDefaultAsync(d => d.Id == id, ct);

    public void Add(SupplementaryDocument document) => db.Documents.Add(document);

    public void Remove(SupplementaryDocument document) => db.Documents.Remove(document);
}
