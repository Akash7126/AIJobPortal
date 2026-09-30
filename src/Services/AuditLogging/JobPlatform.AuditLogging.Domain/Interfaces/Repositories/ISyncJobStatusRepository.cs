namespace JobPlatform.AuditLogging.Domain.Interfaces.Repositories;

public interface ISyncJobStatusRepository
{
    Task<SyncJobStatus?> GetAsync(string platformJobId, CancellationToken ct = default);

    void Add(SyncJobStatus status);
}
