using JobPlatform.CandidateSourcing.Domain.Interfaces.Repositories;
using JobPlatform.CandidateSourcing.Domain.Projection;
using Microsoft.EntityFrameworkCore;

namespace JobPlatform.CandidateSourcing.Infrastructure.Persistence.Repositories;

internal sealed class CandidateProjectionRepository(CandidateSourcingDbContext db) : ICandidateProjectionRepository
{
    public Task<CandidateProjection?> GetAsync(Guid profileId, CancellationToken ct = default) =>
        db.CandidateProjections.FirstOrDefaultAsync(p => p.ProfileId == profileId, ct);

    public Task<CandidateProjection?> GetByOwnerAccountIdAsync(Guid ownerAccountId, CancellationToken ct = default) =>
        db.CandidateProjections.FirstOrDefaultAsync(p => p.OwnerAccountId == ownerAccountId, ct);

    public void Add(CandidateProjection projection) => db.CandidateProjections.Add(projection);
}
