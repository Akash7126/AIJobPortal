using JobPlatform.Notification.Application.Composition;
using JobPlatform.Notification.Application.Interfaces;
using JobPlatform.Notification.Domain;
using JobPlatform.Notification.Domain.Interfaces.Repositories;
using JobPlatform.SharedKernel.Application.Interfaces.Ports;
using JobPlatform.SharedKernel.Common.Enums;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;

namespace JobPlatform.Notification.Application.UnitTests;

/// <summary>In-memory implementation of every repository, so handlers are tested against real state instead of mocks of behaviour.</summary>
public sealed class FakeStore :
    IInAppNotificationRepository, IOutboundMessageRepository, INotificationPreferenceRepository, IEmailTemplateRepository, INotificationTypeRepository,
    ISmsPolicyRepository, IJobConfirmationRepository, IWeeklyCycleRepository
{
    public List<InAppNotification> InApp { get; } = new();
    public List<OutboundMessage> Messages { get; } = new();
    public List<NotificationPreference> Preferences { get; } = new();
    public List<EmailTemplate> Templates { get; } = new();
    public List<NotificationType> Types { get; } = new();
    public List<SmsPolicy> Policies { get; } = new() { SmsPolicy.Initial(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)) };
    public List<JobConfirmation> Confirmations { get; } = new();
    public List<WeeklyCycle> Cycles { get; } = new();

    Task<InAppNotification?> IInAppNotificationRepository.GetAsync(Guid id, CancellationToken ct) => Task.FromResult(InApp.FirstOrDefault(n => n.Id == id));
    void IInAppNotificationRepository.Add(InAppNotification n) => InApp.Add(n);

    Task<OutboundMessage?> IOutboundMessageRepository.GetAsync(Guid id, CancellationToken ct) => Task.FromResult(Messages.FirstOrDefault(m => m.Id == id));
    public Task<bool> ExistsByDedupeKeyAsync(string key, CancellationToken ct = default) => Task.FromResult(Messages.Any(m => m.DedupeKey == key));
    public Task<OutboundMessage?> GetByProviderMessageIdAsync(string id, CancellationToken ct = default) => Task.FromResult(Messages.FirstOrDefault(m => m.ProviderMessageId == id));
    public Task<IReadOnlyList<OutboundMessage>> ListDueAsync(DateTime now, int take, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<OutboundMessage>>(Messages.Where(m => m.IsDue(now)).Take(take).ToList());
    public Task<IReadOnlyList<OutboundMessage>> ListDigestCandidatesAsync(DateTime before, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<OutboundMessage>>(Messages.Where(m => m.Status == MessageStatus.Pending && m.IsDigestCandidate && m.CreatedAtUtc <= before).ToList());
    void IOutboundMessageRepository.Add(OutboundMessage m) => Messages.Add(m);

    Task<NotificationPreference?> INotificationPreferenceRepository.GetAsync(Guid id, CancellationToken ct) => Task.FromResult(Preferences.FirstOrDefault(p => p.Id == id));
    void INotificationPreferenceRepository.Add(NotificationPreference p) => Preferences.Add(p);

    public Task<EmailTemplate?> GetCurrentAsync(string code, string locale, CancellationToken ct = default) =>
        Task.FromResult(Templates.Where(t => t.Code == code && t.Locale == locale).OrderByDescending(t => t.Version).FirstOrDefault());
    public Task<EmailTemplate?> GetVersionAsync(string code, string locale, int version, CancellationToken ct = default) =>
        Task.FromResult(Templates.FirstOrDefault(t => t.Code == code && t.Locale == locale && t.Version == version));
    void IEmailTemplateRepository.Add(EmailTemplate t) => Templates.Add(t);

    Task<NotificationType?> INotificationTypeRepository.GetAsync(string code, CancellationToken ct) => Task.FromResult(Types.FirstOrDefault(t => t.Id == code));
    public Task<IReadOnlyList<NotificationType>> ListAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<NotificationType>>(Types.ToList());
    void INotificationTypeRepository.Add(NotificationType t) => Types.Add(t);

    Task<SmsPolicy> ISmsPolicyRepository.GetCurrentAsync(CancellationToken ct) => Task.FromResult(Policies.OrderByDescending(p => p.Id).First());
    void ISmsPolicyRepository.Add(SmsPolicy p) => Policies.Add(p);

    Task<JobConfirmation?> IJobConfirmationRepository.GetAsync(Guid platform, string job, CancellationToken ct) =>
        Task.FromResult(Confirmations.FirstOrDefault(c => c.SourcePlatformId == platform && c.SourceJobId == job));
    void IJobConfirmationRepository.Add(JobConfirmation c) => Confirmations.Add(c);

    Task<bool> IWeeklyCycleRepository.ExistsAsync(Guid account, string week, CancellationToken ct) => Task.FromResult(Cycles.Any(c => c.AccountId == account && c.IsoWeek == week));
    void IWeeklyCycleRepository.Add(WeeklyCycle c) => Cycles.Add(c);

    public NotificationComposer Composer(FakeTimeProvider clock, NotificationOptions? options = null) =>
        new(this, this, this, this, this, Options.Create(options ?? new NotificationOptions()), new FakeTokens(), clock);
}

public sealed class FakeTokens : IUnsubscribeTokens
{
    public string Create(Guid accountId, string category) => $"tok-{accountId:N}-{category}";

    public bool TryParse(string token, out Guid accountId, out string category)
    {
        accountId = Guid.Empty;
        category = string.Empty;
        var parts = token.Split('-', 3);
        if (parts.Length != 3 || parts[0] != "tok" || !Guid.TryParseExact(parts[1], "N", out accountId))
        {
            return false;
        }

        category = parts[2];
        return true;
    }
}

public static class Users
{
    public static ICurrentUser Of(ActorType? actor, Guid? id)
    {
        var user = Substitute.For<ICurrentUser>();
        user.IsAuthenticated.Returns(actor is not null);
        user.ActorType.Returns(actor);
        user.UserId.Returns(id);
        return user;
    }
}

public static class Clocks
{
    public static FakeTimeProvider At(int year = 2026, int month = 5, int day = 6) => new(new DateTimeOffset(year, month, day, 9, 0, 0, TimeSpan.Zero));
}
