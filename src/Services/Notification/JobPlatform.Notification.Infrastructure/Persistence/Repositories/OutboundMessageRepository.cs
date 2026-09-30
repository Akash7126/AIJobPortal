using JobPlatform.Notification.Domain;
using JobPlatform.Notification.Domain.Interfaces.Repositories;
using Microsoft.EntityFrameworkCore;

namespace JobPlatform.Notification.Infrastructure.Persistence.Repositories;

internal sealed class OutboundMessageRepository : IOutboundMessageRepository
{
    private readonly NotificationDbContext _db;

    public OutboundMessageRepository(NotificationDbContext db) => _db = db;

    public Task<OutboundMessage?> GetAsync(Guid id, CancellationToken ct = default) => _db.OutboundMessages.FirstOrDefaultAsync(m => m.Id == id, ct);

    public async Task<bool> ExistsByDedupeKeyAsync(string dedupeKey, CancellationToken ct = default) =>
        _db.OutboundMessages.Local.Any(m => m.DedupeKey == dedupeKey) || await _db.OutboundMessages.AnyAsync(m => m.DedupeKey == dedupeKey, ct);

    public Task<OutboundMessage?> GetByProviderMessageIdAsync(string providerMessageId, CancellationToken ct = default) =>
        _db.OutboundMessages.FirstOrDefaultAsync(m => m.ProviderMessageId == providerMessageId, ct);

    public async Task<IReadOnlyList<OutboundMessage>> ListDueAsync(DateTime nowUtc, int take, CancellationToken ct = default) =>
        await _db.OutboundMessages.Where(m => m.Status == MessageStatus.Pending && !m.IsDigestCandidate && m.NextAttemptUtc <= nowUtc)
            .OrderBy(m => m.NextAttemptUtc).Take(take).AsNoTracking().ToListAsync(ct);

    public async Task<IReadOnlyList<OutboundMessage>> ListDigestCandidatesAsync(DateTime createdBeforeUtc, CancellationToken ct = default) =>
        await _db.OutboundMessages.Where(m => m.Status == MessageStatus.Pending && m.IsDigestCandidate && m.CreatedAtUtc <= createdBeforeUtc).ToListAsync(ct);

    public void Add(OutboundMessage message) => _db.OutboundMessages.Add(message);
}
