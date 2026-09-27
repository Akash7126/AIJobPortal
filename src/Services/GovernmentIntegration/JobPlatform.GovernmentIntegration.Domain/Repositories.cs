namespace JobPlatform.GovernmentIntegration.Domain;

/// <summary>Aggregate-oriented repositories (foundation section 8): no IQueryable leaks, no SaveChanges (the unit of work commits).</summary>
public interface IEmployerVerificationRepository
{
    Task<EmployerVerification?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>The verification that is still Pending or PendingManualReview for this employer, if any (one-active-per-employer, section 8.1).</summary>
    Task<EmployerVerification?> GetActiveByEmployerAsync(Guid employerAccountId, CancellationToken ct = default);

    Task<bool> ExistsActiveAsync(Guid employerAccountId, CancellationToken ct = default);

    void Add(EmployerVerification verification);
}

public interface IGovernmentVerificationDataRepository
{
    Task<GovernmentVerificationData?> GetByIdAsync(Guid id, CancellationToken ct = default);

    Task<GovernmentVerificationData?> GetLatestForSubjectAsync(SubjectType subjectType, Guid subjectId, CancellationToken ct = default);

    Task<IReadOnlyList<GovernmentVerificationData>> ListExpiredAsync(DateTime now, int take, CancellationToken ct = default);

    void Add(GovernmentVerificationData data);
}

public interface IEducationalCredentialVerificationRepository
{
    Task<EducationalCredentialVerification?> GetByIdAsync(Guid id, CancellationToken ct = default);

    Task<EducationalCredentialVerification?> GetLatestForSubjectAsync(Guid subjectId, CancellationToken ct = default);

    Task<IReadOnlyList<EducationalCredentialVerification>> ListExpiredAsync(DateTime now, int take, CancellationToken ct = default);

    void Add(EducationalCredentialVerification verification);
}

public interface IIdentityVerificationRepository
{
    Task<IdentityVerificationData?> GetByIdAsync(Guid id, CancellationToken ct = default);

    Task<IdentityVerificationData?> GetLatestForSubjectAsync(Guid subjectId, CancellationToken ct = default);

    Task<IReadOnlyList<IdentityVerificationData>> ListExpiredAsync(DateTime now, int take, CancellationToken ct = default);

    void Add(IdentityVerificationData verification);
}

public interface ILegacyDataRepository
{
    Task<LegacyData?> GetByIdAsync(Guid id, CancellationToken ct = default);

    Task<LegacyData?> GetBySourceKeyAsync(SourceSystem sourceSystem, string sourceRecordId, CancellationToken ct = default);

    Task<IReadOnlyList<LegacyData>> ListByBatchAsync(Guid migrationBatchId, CancellationToken ct = default);

    void Add(LegacyData data);
}

public interface IDataQualityRepository
{
    Task<DataQuality?> GetByIdAsync(Guid id, CancellationToken ct = default);

    Task<DataQuality?> GetByBatchAsync(Guid batchId, CancellationToken ct = default);

    /// <summary>INV-13: a new cleansing run must not start while one is already Running for the same batch.</summary>
    Task<bool> HasRunningForBatchAsync(Guid batchId, CancellationToken ct = default);

    void Add(DataQuality dataQuality);
}

public interface IMigrationRunRepository
{
    Task<MigrationRun?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>Single-run guard (handover section 3.7): a run that is Created/Running/PhaseFailed, if any.</summary>
    Task<MigrationRun?> GetActiveAsync(CancellationToken ct = default);

    void Add(MigrationRun run);
}

public interface IGovernmentSourceConnectionRepository
{
    Task<GovernmentSourceConnection?> GetByIdAsync(Guid id, CancellationToken ct = default);

    Task<GovernmentSourceConnection?> GetBySourceAsync(SourceSystem source, CancellationToken ct = default);

    Task<IReadOnlyList<GovernmentSourceConnection>> ListAsync(CancellationToken ct = default);

    void Add(GovernmentSourceConnection connection);
}

public interface IKnownAccountRepository
{
    Task<KnownAccount?> GetAsync(Guid accountId, CancellationToken ct = default);

    void Add(KnownAccount account);
}

public interface IGovernmentDataAccessLogRepository
{
    void Add(GovernmentDataAccessLogEntry entry);
}
