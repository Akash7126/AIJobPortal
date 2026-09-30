namespace JobPlatform.AuditLogging.Domain.Interfaces.Repositories;

public interface IJobStatusHistoryRepository
{
    Task<bool> ExistsAsync(Guid sourceMessageId, CancellationToken ct = default);

    void Add(JobStatusHistoryEntry entry);
}
