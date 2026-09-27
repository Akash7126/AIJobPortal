namespace JobPlatform.Notification.Domain;

public interface IInAppNotificationRepository
{
    Task<InAppNotification?> GetAsync(Guid id, CancellationToken ct = default);

    void Add(InAppNotification notification);
}

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

public interface INotificationPreferenceRepository
{
    /// <summary>The stored preferences, or null when the user never saved any (callers then use <see cref="NotificationPreference.Default"/>).</summary>
    Task<NotificationPreference?> GetAsync(Guid accountId, CancellationToken ct = default);

    void Add(NotificationPreference preference);
}

public interface IEmailTemplateRepository
{
    Task<EmailTemplate?> GetCurrentAsync(string code, string locale, CancellationToken ct = default);

    Task<EmailTemplate?> GetVersionAsync(string code, string locale, int version, CancellationToken ct = default);

    void Add(EmailTemplate template);
}

public interface INotificationTypeRepository
{
    Task<NotificationType?> GetAsync(string code, CancellationToken ct = default);

    Task<IReadOnlyList<NotificationType>> ListAsync(CancellationToken ct = default);

    void Add(NotificationType type);
}

public interface ISmsPolicyRepository
{
    Task<SmsPolicy> GetCurrentAsync(CancellationToken ct = default);

    void Add(SmsPolicy policy);
}

public interface IJobConfirmationRepository
{
    Task<JobConfirmation?> GetAsync(Guid sourcePlatformId, string sourceJobId, CancellationToken ct = default);

    void Add(JobConfirmation confirmation);
}

public interface IWeeklyCycleRepository
{
    Task<bool> ExistsAsync(Guid accountId, string isoWeek, CancellationToken ct = default);

    void Add(WeeklyCycle cycle);
}
