namespace JobPlatform.Notification.Domain.Interfaces.Repositories;

public interface IOutboundMessageRepository
{
    Task<OutboundMessage?> GetAsync(Guid id, CancellationToken ct = default);

    Task<bool> ExistsByDedupeKeyAsync(string dedupeKey, CancellationToken ct = default);

    Task<OutboundMessage?> GetByProviderMessageIdAsync(string providerMessageId, CancellationToken ct = default);

    /// <summary>Pending, non-digest messages whose next attempt is due.</summary>
    Task<IReadOnlyList<OutboundMessage>> ListDueAsync(DateTime nowUtc, int take, CancellationToken ct = default);

    /// <summary>Pending digest candidates created before the cut-off (grouped per recipient by the caller).</summary>
    Task<IReadOnlyList<OutboundMessage>> ListDigestCandidatesAsync(DateTime createdBeforeUtc, CancellationToken ct = default);

    void Add(OutboundMessage message);
}
