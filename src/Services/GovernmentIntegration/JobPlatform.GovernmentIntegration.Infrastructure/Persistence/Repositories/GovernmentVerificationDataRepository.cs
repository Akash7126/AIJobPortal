using JobPlatform.GovernmentIntegration.Domain;
using JobPlatform.GovernmentIntegration.Domain.Interfaces.Repositories;
using Microsoft.EntityFrameworkCore;

namespace JobPlatform.GovernmentIntegration.Infrastructure.Persistence.Repositories;

internal sealed class GovernmentVerificationDataRepository(GovernmentIntegrationDbContext db) : IGovernmentVerificationDataRepository
{
    public Task<GovernmentVerificationData?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        db.GovernmentVerificationData.FirstOrDefaultAsync(d => d.Id == id, ct);

    public Task<GovernmentVerificationData?> GetLatestForSubjectAsync(SubjectType subjectType, Guid subjectId, CancellationToken ct = default) =>
        db.GovernmentVerificationData.Where(d => d.Subject.SubjectType == subjectType && d.Subject.SubjectId == subjectId)
            .OrderByDescending(d => d.ImportedAtUtc).FirstOrDefaultAsync(ct);

    public async Task<IReadOnlyList<GovernmentVerificationData>> ListExpiredAsync(DateTime now, int take, CancellationToken ct = default) =>
        await db.GovernmentVerificationData.Where(d => d.Status == GovernmentDataStatus.Verified && d.RetentionExpiresAtUtc <= now)
            .OrderBy(d => d.RetentionExpiresAtUtc).Take(take).ToListAsync(ct);

    public void Add(GovernmentVerificationData data) => db.GovernmentVerificationData.Add(data);
}
