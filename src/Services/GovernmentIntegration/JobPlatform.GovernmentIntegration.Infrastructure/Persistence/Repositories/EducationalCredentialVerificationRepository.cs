using JobPlatform.GovernmentIntegration.Domain;
using JobPlatform.GovernmentIntegration.Domain.Interfaces.Repositories;
using Microsoft.EntityFrameworkCore;

namespace JobPlatform.GovernmentIntegration.Infrastructure.Persistence.Repositories;

internal sealed class EducationalCredentialVerificationRepository(GovernmentIntegrationDbContext db) : IEducationalCredentialVerificationRepository
{
    public Task<EducationalCredentialVerification?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        db.EducationalCredentialVerifications.FirstOrDefaultAsync(e => e.Id == id, ct);

    public Task<EducationalCredentialVerification?> GetLatestForSubjectAsync(Guid subjectId, CancellationToken ct = default) =>
        db.EducationalCredentialVerifications.Where(e => e.SubjectId == subjectId).OrderByDescending(e => e.Id).FirstOrDefaultAsync(ct);

    public async Task<IReadOnlyList<EducationalCredentialVerification>> ListExpiredAsync(DateTime now, int take, CancellationToken ct = default) =>
        await db.EducationalCredentialVerifications.Where(e => e.RetentionExpiresAtUtc <= now).OrderBy(e => e.RetentionExpiresAtUtc).Take(take)
            .ToListAsync(ct);

    public void Add(EducationalCredentialVerification verification) => db.EducationalCredentialVerifications.Add(verification);
}
