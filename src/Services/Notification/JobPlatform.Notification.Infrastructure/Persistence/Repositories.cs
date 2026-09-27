using JobPlatform.Notification.Domain;
using Microsoft.EntityFrameworkCore;

namespace JobPlatform.Notification.Infrastructure.Persistence;

/// <summary>Aggregate-oriented repositories: no IQueryable leaks, no SaveChanges (the unit of work commits).</summary>
internal sealed class InAppNotificationRepository : IInAppNotificationRepository
{
    private readonly NotificationDbContext _db;

    public InAppNotificationRepository(NotificationDbContext db) => _db = db;

    public Task<InAppNotification?> GetAsync(Guid id, CancellationToken ct = default) => _db.InAppNotifications.FirstOrDefaultAsync(n => n.Id == id, ct);

    public void Add(InAppNotification notification) => _db.InAppNotifications.Add(notification);
}

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

internal sealed class NotificationPreferenceRepository : INotificationPreferenceRepository
{
    private readonly NotificationDbContext _db;

    public NotificationPreferenceRepository(NotificationDbContext db) => _db = db;

    public async Task<NotificationPreference?> GetAsync(Guid accountId, CancellationToken ct = default) =>
        _db.NotificationPreferences.Local.FirstOrDefault(p => p.Id == accountId) ?? await _db.NotificationPreferences.FirstOrDefaultAsync(p => p.Id == accountId, ct);

    public void Add(NotificationPreference preference) => _db.NotificationPreferences.Add(preference);
}

internal sealed class EmailTemplateRepository : IEmailTemplateRepository
{
    private readonly NotificationDbContext _db;

    public EmailTemplateRepository(NotificationDbContext db) => _db = db;

    public Task<EmailTemplate?> GetCurrentAsync(string code, string locale, CancellationToken ct = default) =>
        _db.EmailTemplates.Where(t => t.Code == code && t.Locale == locale).OrderByDescending(t => t.Version).FirstOrDefaultAsync(ct);

    public Task<EmailTemplate?> GetVersionAsync(string code, string locale, int version, CancellationToken ct = default) =>
        _db.EmailTemplates.FirstOrDefaultAsync(t => t.Code == code && t.Locale == locale && t.Version == version, ct);

    public void Add(EmailTemplate template) => _db.EmailTemplates.Add(template);
}

internal sealed class NotificationTypeRepository : INotificationTypeRepository
{
    private readonly NotificationDbContext _db;

    public NotificationTypeRepository(NotificationDbContext db) => _db = db;

    public Task<NotificationType?> GetAsync(string code, CancellationToken ct = default) => _db.NotificationTypes.FirstOrDefaultAsync(t => t.Id == code, ct);

    public async Task<IReadOnlyList<NotificationType>> ListAsync(CancellationToken ct = default) =>
        await _db.NotificationTypes.AsNoTracking().OrderBy(t => t.Id).ToListAsync(ct);

    public void Add(NotificationType type) => _db.NotificationTypes.Add(type);
}

internal sealed class SmsPolicyRepository : ISmsPolicyRepository
{
    private readonly NotificationDbContext _db;

    public SmsPolicyRepository(NotificationDbContext db) => _db = db;

    public async Task<SmsPolicy> GetCurrentAsync(CancellationToken ct = default) =>
        await _db.SmsPolicies.OrderByDescending(p => p.Id).FirstOrDefaultAsync(ct) ?? SmsPolicy.Initial(DateTime.UtcNow);

    public void Add(SmsPolicy policy) => _db.SmsPolicies.Add(policy);
}

internal sealed class JobConfirmationRepository : IJobConfirmationRepository
{
    private readonly NotificationDbContext _db;

    public JobConfirmationRepository(NotificationDbContext db) => _db = db;

    public Task<JobConfirmation?> GetAsync(Guid sourcePlatformId, string sourceJobId, CancellationToken ct = default) =>
        _db.JobConfirmations.FirstOrDefaultAsync(c => c.SourcePlatformId == sourcePlatformId && c.SourceJobId == sourceJobId, ct);

    public void Add(JobConfirmation confirmation) => _db.JobConfirmations.Add(confirmation);
}

internal sealed class WeeklyCycleRepository : IWeeklyCycleRepository
{
    private readonly NotificationDbContext _db;

    public WeeklyCycleRepository(NotificationDbContext db) => _db = db;

    public async Task<bool> ExistsAsync(Guid accountId, string isoWeek, CancellationToken ct = default) =>
        _db.WeeklyCycles.Local.Any(c => c.AccountId == accountId && c.IsoWeek == isoWeek) || await _db.WeeklyCycles.AnyAsync(c => c.AccountId == accountId && c.IsoWeek == isoWeek, ct);

    public void Add(WeeklyCycle cycle) => _db.WeeklyCycles.Add(cycle);
}
