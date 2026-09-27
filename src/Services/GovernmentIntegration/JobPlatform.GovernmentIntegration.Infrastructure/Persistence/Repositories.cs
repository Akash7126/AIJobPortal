using JobPlatform.GovernmentIntegration.Domain;
using Microsoft.EntityFrameworkCore;

namespace JobPlatform.GovernmentIntegration.Infrastructure.Persistence;

internal sealed class EmployerVerificationRepository(GovernmentIntegrationDbContext db) : IEmployerVerificationRepository
{
    private static readonly VerificationState[] ActiveStates = { VerificationState.Pending, VerificationState.PendingManualReview };

    public Task<EmployerVerification?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        db.EmployerVerifications.Include(v => v.Attempts).FirstOrDefaultAsync(v => v.Id == id, ct);

    public Task<EmployerVerification?> GetActiveByEmployerAsync(Guid employerAccountId, CancellationToken ct = default) =>
        db.EmployerVerifications.Include(v => v.Attempts)
            .FirstOrDefaultAsync(v => v.EmployerAccountId == employerAccountId && ActiveStates.Contains(v.State), ct);

    public Task<bool> ExistsActiveAsync(Guid employerAccountId, CancellationToken ct = default) =>
        db.EmployerVerifications.AnyAsync(v => v.EmployerAccountId == employerAccountId && ActiveStates.Contains(v.State), ct);

    public void Add(EmployerVerification verification) => db.EmployerVerifications.Add(verification);
}

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

internal sealed class LegacyDataRepository(GovernmentIntegrationDbContext db) : ILegacyDataRepository
{
    public Task<LegacyData?> GetByIdAsync(Guid id, CancellationToken ct = default) => db.LegacyData.FirstOrDefaultAsync(l => l.Id == id, ct);

    public Task<LegacyData?> GetBySourceKeyAsync(SourceSystem sourceSystem, string sourceRecordId, CancellationToken ct = default) =>
        db.LegacyData.FirstOrDefaultAsync(l => l.SourceSystem == sourceSystem && l.SourceRecordId == sourceRecordId, ct);

    public async Task<IReadOnlyList<LegacyData>> ListByBatchAsync(Guid migrationBatchId, CancellationToken ct = default) =>
        await db.LegacyData.Where(l => l.MigrationBatchId == migrationBatchId).ToListAsync(ct);

    public void Add(LegacyData data) => db.LegacyData.Add(data);
}

internal sealed class DataQualityRepository(GovernmentIntegrationDbContext db) : IDataQualityRepository
{
    public Task<DataQuality?> GetByIdAsync(Guid id, CancellationToken ct = default) => db.DataQuality.FirstOrDefaultAsync(d => d.Id == id, ct);

    public Task<DataQuality?> GetByBatchAsync(Guid batchId, CancellationToken ct = default) =>
        db.DataQuality.Where(d => d.BatchId == batchId).OrderByDescending(d => d.Id).FirstOrDefaultAsync(ct);

    public Task<bool> HasRunningForBatchAsync(Guid batchId, CancellationToken ct = default) =>
        db.DataQuality.AnyAsync(d => d.BatchId == batchId && d.Status == DataQualityStatus.Running, ct);

    public void Add(DataQuality dataQuality) => db.DataQuality.Add(dataQuality);
}

internal sealed class MigrationRunRepository(GovernmentIntegrationDbContext db) : IMigrationRunRepository
{
    private static readonly MigrationStatus[] ActiveStatuses = { MigrationStatus.Created, MigrationStatus.Running, MigrationStatus.PhaseFailed };

    public Task<MigrationRun?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        db.MigrationRuns.Include(r => r.Phases).Include(r => r.Log).FirstOrDefaultAsync(r => r.Id == id, ct);

    public Task<MigrationRun?> GetActiveAsync(CancellationToken ct = default) =>
        db.MigrationRuns.Include(r => r.Phases).Include(r => r.Log).FirstOrDefaultAsync(r => ActiveStatuses.Contains(r.Status), ct);

    public void Add(MigrationRun run) => db.MigrationRuns.Add(run);
}

internal sealed class GovernmentSourceConnectionRepository(GovernmentIntegrationDbContext db) : IGovernmentSourceConnectionRepository
{
    public Task<GovernmentSourceConnection?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        db.GovernmentSourceConnections.FirstOrDefaultAsync(c => c.Id == id, ct);

    public Task<GovernmentSourceConnection?> GetBySourceAsync(SourceSystem source, CancellationToken ct = default) =>
        db.GovernmentSourceConnections.FirstOrDefaultAsync(c => c.Source == source, ct);

    public async Task<IReadOnlyList<GovernmentSourceConnection>> ListAsync(CancellationToken ct = default) =>
        await db.GovernmentSourceConnections.OrderBy(c => c.Source).ToListAsync(ct);

    public void Add(GovernmentSourceConnection connection) => db.GovernmentSourceConnections.Add(connection);
}

internal sealed class KnownAccountRepository(GovernmentIntegrationDbContext db) : IKnownAccountRepository
{
    public Task<KnownAccount?> GetAsync(Guid accountId, CancellationToken ct = default) =>
        db.KnownAccounts.FirstOrDefaultAsync(a => a.AccountId == accountId, ct);

    public void Add(KnownAccount account) => db.KnownAccounts.Add(account);
}

internal sealed class GovernmentDataAccessLogRepository(GovernmentIntegrationDbContext db) : IGovernmentDataAccessLogRepository
{
    public void Add(GovernmentDataAccessLogEntry entry) => db.GovernmentDataAccessLog.Add(entry);
}
