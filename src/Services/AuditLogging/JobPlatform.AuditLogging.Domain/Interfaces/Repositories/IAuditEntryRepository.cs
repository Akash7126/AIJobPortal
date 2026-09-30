namespace JobPlatform.AuditLogging.Domain.Interfaces.Repositories;

/// <summary>Aggregate-oriented repositories. The read side never uses them (see IAuditReadStore in the application layer).</summary>
public interface IAuditEntryRepository
{
    /// <summary>INV-01: one entry per (source BC, source message id, category), so a redelivery never duplicates a log line.</summary>
    Task<bool> ExistsAsync(string sourceBc, Guid sourceMessageId, AuditCategory category, CancellationToken ct = default);

    void Add(AuditEntry entry);

    Task<IReadOnlyList<AuditEntry>> ListExpiredAsync(DateTime nowUtc, int take, CancellationToken ct = default);
}
