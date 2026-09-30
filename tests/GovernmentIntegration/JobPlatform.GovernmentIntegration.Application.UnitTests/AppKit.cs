using JobPlatform.GovernmentIntegration.Application.DTOs.Connections;
using JobPlatform.GovernmentIntegration.Application.DTOs.EmployerVerifications;
using JobPlatform.GovernmentIntegration.Application.DTOs.Migration;
using JobPlatform.GovernmentIntegration.Application.DTOs.Verifications;
using JobPlatform.GovernmentIntegration.Application.Interfaces;
using JobPlatform.GovernmentIntegration.Domain;
using JobPlatform.GovernmentIntegration.Domain.Interfaces.Repositories;
using JobPlatform.SharedKernel.Application.Interfaces.Ports;
using JobPlatform.SharedKernel.Application.Paging;
using JobPlatform.SharedKernel.Common.Enums;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;

namespace JobPlatform.GovernmentIntegration.Application.UnitTests;

/// <summary>In-memory implementation of every repository so handlers are tested against real aggregate state (no mocking of domain behaviour).</summary>
public sealed class FakeStore :
    IEmployerVerificationRepository, IGovernmentVerificationDataRepository, IEducationalCredentialVerificationRepository,
    IIdentityVerificationRepository, ILegacyDataRepository, IDataQualityRepository, IMigrationRunRepository,
    IGovernmentSourceConnectionRepository, IKnownAccountRepository, IGovernmentDataAccessLogRepository
{
    private static readonly VerificationState[] ActiveStates = { VerificationState.Pending, VerificationState.PendingManualReview };
    private static readonly MigrationStatus[] ActiveMigrationStatuses = { MigrationStatus.Created, MigrationStatus.Running, MigrationStatus.PhaseFailed };

    public List<EmployerVerification> EmployerVerifications { get; } = new();
    public List<GovernmentVerificationData> GovernmentVerificationData { get; } = new();
    public List<EducationalCredentialVerification> EducationalCredentialVerifications { get; } = new();
    public List<IdentityVerificationData> IdentityVerifications { get; } = new();
    public List<LegacyData> LegacyData { get; } = new();
    public List<DataQuality> DataQuality { get; } = new();
    public List<MigrationRun> MigrationRuns { get; } = new();
    public List<GovernmentSourceConnection> Connections { get; } = new();
    public List<KnownAccount> KnownAccounts { get; } = new();
    public List<GovernmentDataAccessLogEntry> AccessLog { get; } = new();

    Task<EmployerVerification?> IEmployerVerificationRepository.GetByIdAsync(Guid id, CancellationToken ct) =>
        Task.FromResult(EmployerVerifications.FirstOrDefault(v => v.Id == id));

    public Task<EmployerVerification?> GetActiveByEmployerAsync(Guid employerAccountId, CancellationToken ct = default) =>
        Task.FromResult(EmployerVerifications.FirstOrDefault(v => v.EmployerAccountId == employerAccountId && ActiveStates.Contains(v.State)));

    public Task<bool> ExistsActiveAsync(Guid employerAccountId, CancellationToken ct = default) =>
        Task.FromResult(EmployerVerifications.Any(v => v.EmployerAccountId == employerAccountId && ActiveStates.Contains(v.State)));

    public void Add(EmployerVerification verification) => EmployerVerifications.Add(verification);

    Task<GovernmentVerificationData?> IGovernmentVerificationDataRepository.GetByIdAsync(Guid id, CancellationToken ct) =>
        Task.FromResult(GovernmentVerificationData.FirstOrDefault(d => d.Id == id));

    public Task<GovernmentVerificationData?> GetLatestForSubjectAsync(SubjectType subjectType, Guid subjectId, CancellationToken ct = default) =>
        Task.FromResult(GovernmentVerificationData.LastOrDefault(d => d.Subject.SubjectType == subjectType && d.Subject.SubjectId == subjectId));

    Task<IReadOnlyList<GovernmentVerificationData>> IGovernmentVerificationDataRepository.ListExpiredAsync(DateTime now, int take, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<GovernmentVerificationData>>(
            GovernmentVerificationData.Where(d => d.Status == GovernmentDataStatus.Verified && d.RetentionExpiresAtUtc <= now).Take(take).ToList());

    public void Add(GovernmentVerificationData data) => GovernmentVerificationData.Add(data);

    Task<EducationalCredentialVerification?> IEducationalCredentialVerificationRepository.GetByIdAsync(Guid id, CancellationToken ct) =>
        Task.FromResult(EducationalCredentialVerifications.FirstOrDefault(e => e.Id == id));

    public Task<EducationalCredentialVerification?> GetLatestForSubjectAsync(Guid subjectId, CancellationToken ct = default) =>
        Task.FromResult(EducationalCredentialVerifications.LastOrDefault(e => e.SubjectId == subjectId));

    Task<IReadOnlyList<EducationalCredentialVerification>> IEducationalCredentialVerificationRepository.ListExpiredAsync(DateTime now, int take,
        CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<EducationalCredentialVerification>>(
            EducationalCredentialVerifications.Where(e => e.RetentionExpiresAtUtc <= now).Take(take).ToList());

    public void Add(EducationalCredentialVerification verification) => EducationalCredentialVerifications.Add(verification);

    Task<IdentityVerificationData?> IIdentityVerificationRepository.GetByIdAsync(Guid id, CancellationToken ct) =>
        Task.FromResult(IdentityVerifications.FirstOrDefault(i => i.Id == id));

    Task<IdentityVerificationData?> IIdentityVerificationRepository.GetLatestForSubjectAsync(Guid subjectId, CancellationToken ct) =>
        Task.FromResult(IdentityVerifications.LastOrDefault(i => i.Subject.SubjectId == subjectId));

    Task<IReadOnlyList<IdentityVerificationData>> IIdentityVerificationRepository.ListExpiredAsync(DateTime now, int take, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<IdentityVerificationData>>(IdentityVerifications.Where(i => i.RetentionExpiresAtUtc <= now).Take(take).ToList());

    public void Add(IdentityVerificationData verification) => IdentityVerifications.Add(verification);

    public Task<LegacyData?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult(LegacyData.FirstOrDefault(l => l.Id == id));

    public Task<LegacyData?> GetBySourceKeyAsync(SourceSystem sourceSystem, string sourceRecordId, CancellationToken ct = default) =>
        Task.FromResult(LegacyData.FirstOrDefault(l => l.SourceSystem == sourceSystem && l.SourceRecordId == sourceRecordId));

    public Task<IReadOnlyList<LegacyData>> ListByBatchAsync(Guid migrationBatchId, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<LegacyData>>(LegacyData.Where(l => l.MigrationBatchId == migrationBatchId).ToList());

    public void Add(LegacyData data) => LegacyData.Add(data);

    Task<DataQuality?> IDataQualityRepository.GetByIdAsync(Guid id, CancellationToken ct) => Task.FromResult(DataQuality.FirstOrDefault(d => d.Id == id));

    public Task<DataQuality?> GetByBatchAsync(Guid batchId, CancellationToken ct = default) =>
        Task.FromResult(DataQuality.LastOrDefault(d => d.BatchId == batchId));

    public Task<bool> HasRunningForBatchAsync(Guid batchId, CancellationToken ct = default) =>
        Task.FromResult(DataQuality.Any(d => d.BatchId == batchId && d.Status == DataQualityStatus.Running));

    public void Add(DataQuality dataQuality) => DataQuality.Add(dataQuality);

    Task<MigrationRun?> IMigrationRunRepository.GetByIdAsync(Guid id, CancellationToken ct) => Task.FromResult(MigrationRuns.FirstOrDefault(r => r.Id == id));

    public Task<MigrationRun?> GetActiveAsync(CancellationToken ct = default) =>
        Task.FromResult(MigrationRuns.FirstOrDefault(r => ActiveMigrationStatuses.Contains(r.Status)));

    public void Add(MigrationRun run) => MigrationRuns.Add(run);

    Task<GovernmentSourceConnection?> IGovernmentSourceConnectionRepository.GetByIdAsync(Guid id, CancellationToken ct) =>
        Task.FromResult(Connections.FirstOrDefault(c => c.Id == id));

    public Task<GovernmentSourceConnection?> GetBySourceAsync(SourceSystem source, CancellationToken ct = default) =>
        Task.FromResult(Connections.FirstOrDefault(c => c.Source == source));

    public Task<IReadOnlyList<GovernmentSourceConnection>> ListAsync(CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<GovernmentSourceConnection>>(Connections.ToList());

    public void Add(GovernmentSourceConnection connection) => Connections.Add(connection);

    public Task<KnownAccount?> GetAsync(Guid accountId, CancellationToken ct = default) =>
        Task.FromResult(KnownAccounts.FirstOrDefault(a => a.AccountId == accountId));

    public void Add(KnownAccount account) => KnownAccounts.Add(account);

    public void Add(GovernmentDataAccessLogEntry entry) => AccessLog.Add(entry);
}

/// <summary>Configurable fake anti-corruption ports (the default stub adapters always Match; tests need every outcome).</summary>
public sealed class FakeMolRegistryClient : IMolRegistryClient
{
    public SourceCallOutcome NextEmployerOutcome { get; set; } = SourceCallOutcome.Match;
    public string? NextErrorCode { get; set; }
    public SourceCallOutcome NextSubjectOutcome { get; set; } = SourceCallOutcome.Match;

    public Task<EmployerVerificationCheckResult> VerifyEmployerAsync(Submission submission, CancellationToken ct) =>
        Task.FromResult(new EmployerVerificationCheckResult(NextEmployerOutcome, NextErrorCode));

    public Task<GovernmentDataCheckResult> CheckSubjectAsync(Subject subject, AccessPurpose purpose, CancellationToken ct) =>
        Task.FromResult(new GovernmentDataCheckResult(NextSubjectOutcome,
            NextSubjectOutcome == SourceCallOutcome.Match ? new Dictionary<string, string> { ["k"] = "v" } : null, NextErrorCode));

    public Task<SourceSyncResult> SyncAsync(CancellationToken ct) => Task.FromResult(new SourceSyncResult(true, "snapshot-1", null));
}

public sealed class FakePefClient : IPefClient
{
    public SourceCallOutcome NextOutcome { get; set; } = SourceCallOutcome.Match;

    public Task<GovernmentDataCheckResult> CheckSubjectAsync(Subject subject, AccessPurpose purpose, CancellationToken ct) =>
        Task.FromResult(new GovernmentDataCheckResult(NextOutcome, NextOutcome == SourceCallOutcome.Match ? new Dictionary<string, string>() : null, null));

    public Task<SourceSyncResult> SyncAsync(CancellationToken ct) => Task.FromResult(new SourceSyncResult(true, "snapshot-2", null));
}

public sealed class FakeGovernmentDatabaseClient : IGovernmentDatabaseClient
{
    public SourceCallOutcome NextOutcome { get; set; } = SourceCallOutcome.Match;

    public Task<GovernmentDataCheckResult> CheckSubjectAsync(Subject subject, AccessPurpose purpose, CancellationToken ct) =>
        Task.FromResult(new GovernmentDataCheckResult(NextOutcome, NextOutcome == SourceCallOutcome.Match ? new Dictionary<string, string>() : null, null));
}

public sealed class FakeEducationalInstitutionClient : IEducationalInstitutionClient
{
    public SourceCallOutcome NextOutcome { get; set; } = SourceCallOutcome.Match;

    public Task<EducationalCheckResult> CheckAsync(Credential credential, CancellationToken ct) =>
        Task.FromResult(new EducationalCheckResult(NextOutcome, null));
}

public sealed class FakeGovernmentIdClient : IGovernmentIdClient
{
    public SourceCallOutcome NextOutcome { get; set; } = SourceCallOutcome.Match;
    public bool NextAmbiguous { get; set; }

    public Task<IdentityCheckResult> CheckAsync(IdentityClaim claim, CancellationToken ct) =>
        Task.FromResult(new IdentityCheckResult(NextOutcome, NextAmbiguous, null));
}

public sealed class FakeLegacySourceReader : ILegacySourceReader
{
    public IReadOnlyList<LegacySourceRecord> NextBatch { get; set; } = Array.Empty<LegacySourceRecord>();

    public Task<IReadOnlyList<LegacySourceRecord>> ReadBatchAsync(SourceSystem sourceSystem, int take, CancellationToken ct) =>
        Task.FromResult(NextBatch);
}

public sealed class FakeReadStore : IGovernmentIntegrationReadStore
{
    public Func<Guid, EmployerVerificationView?> EmployerVerification { get; set; } = _ => null;

    public Task<EmployerVerificationView?> GetEmployerVerificationAsync(Guid id, CancellationToken ct = default) =>
        Task.FromResult(EmployerVerification(id));

    public Task<EmployerVerificationView?> GetActiveEmployerVerificationForEmployerAsync(Guid employerAccountId, CancellationToken ct = default) =>
        Task.FromResult<EmployerVerificationView?>(null);

    public Task<PagedResult<EmployerVerificationView>> ListPendingManualReviewAsync(PageRequest page, CancellationToken ct = default) =>
        Task.FromResult(new PagedResult<EmployerVerificationView>(Array.Empty<EmployerVerificationView>(), page.Page, page.PageSize, 0));

    public Task<SubjectVerificationStatusView> GetSubjectVerificationStatusAsync(SubjectType subjectType, Guid subjectId, CancellationToken ct = default) =>
        Task.FromResult(new SubjectVerificationStatusView(subjectType.ToString(), subjectId, null, null, null));

    public Task<IReadOnlyList<GovernmentSourceConnectionView>> ListGovernmentSourceConnectionsAsync(CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<GovernmentSourceConnectionView>>(Array.Empty<GovernmentSourceConnectionView>());

    public Task<MigrationRunView?> GetMigrationRunAsync(Guid id, CancellationToken ct = default) => Task.FromResult<MigrationRunView?>(null);

    public Task<DataQualityView?> GetDataQualityByBatchAsync(Guid batchId, CancellationToken ct = default) => Task.FromResult<DataQualityView?>(null);

    public Task<PagedResult<GovernmentAccessLogView>> ListGovernmentExchangesAsync(DateTime? from, DateTime? to, PageRequest page,
        CancellationToken ct = default) =>
        Task.FromResult(new PagedResult<GovernmentAccessLogView>(Array.Empty<GovernmentAccessLogView>(), page.Page, page.PageSize, 0));
}

public static class Kit
{
    public static FakeTimeProvider Clock() => new(new DateTimeOffset(2026, 3, 1, 9, 0, 0, TimeSpan.Zero));

    public static ICurrentUser User(ActorType? actor = ActorType.Employer, Guid? id = null)
    {
        var user = Substitute.For<ICurrentUser>();
        user.IsAuthenticated.Returns(actor is not null);
        user.ActorType.Returns(actor);
        user.UserId.Returns(id ?? Guid.NewGuid());
        user.MfaVerified.Returns(actor == ActorType.Administrator);
        return user;
    }
}
