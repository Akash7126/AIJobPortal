using JobPlatform.GovernmentIntegration.Domain;
using JobPlatform.GovernmentIntegration.Domain.Interfaces.Repositories;
using Microsoft.EntityFrameworkCore;

namespace JobPlatform.GovernmentIntegration.Infrastructure.Persistence.Repositories;

internal sealed class IdentityVerificationRepository(GovernmentIntegrationDbContext db) : IIdentityVerificationRepository
{
    public Task<IdentityVerificationData?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        db.IdentityVerifications.FirstOrDefaultAsync(i => i.Id == id, ct);

    public Task<IdentityVerificationData?> GetLatestForSubjectAsync(Guid subjectId, CancellationToken ct = default) =>
        db.IdentityVerifications.Where(i => i.Subject.SubjectId == subjectId).OrderByDescending(i => i.Id).FirstOrDefaultAsync(ct);

    public async Task<IReadOnlyList<IdentityVerificationData>> ListExpiredAsync(DateTime now, int take, CancellationToken ct = default) =>
        await db.IdentityVerifications.Where(i => i.RetentionExpiresAtUtc <= now).OrderBy(i => i.RetentionExpiresAtUtc).Take(take).ToListAsync(ct);

    public void Add(IdentityVerificationData verification) => db.IdentityVerifications.Add(verification);
}
