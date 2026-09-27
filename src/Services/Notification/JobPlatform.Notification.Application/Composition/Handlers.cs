using JobPlatform.Notification.Domain;
using JobPlatform.SharedKernel.Common.ValueObjects;
using JobPlatform.SharedKernel.IntegrationEvents.AccountIdentity;
using JobPlatform.SharedKernel.IntegrationEvents.AiMatching;
using JobPlatform.SharedKernel.IntegrationEvents.ExternalIntegration;
using JobPlatform.SharedKernel.IntegrationEvents.JobPosting;
using JobPlatform.SharedKernel.Messaging;

namespace JobPlatform.Notification.Application.Composition;

// Inbox handlers of BC-13 (handover 5.2). Application use cases: they compose through the NotificationComposer (never a synchronous call to another BC
// inside the message transaction) and are safe to run twice (dedupe key, weekly cycle, confirmation record).

/// <summary>SavedSearchMatched (BC-09, gap G-11): tell the job seeker a new posting matches an opted-in saved search.</summary>
public sealed class NotifySavedSearchMatchHandler(NotificationComposer composer) : IIntegrationEventHandler<SavedSearchMatchedIntegrationEvent>
{
    public Task Handle(SavedSearchMatchedIntegrationEvent e, CancellationToken ct) =>
        composer.ComposeAsync(new ComposeRequest(e.JobSeekerId, Categories.SavedSearchMatch, "saved-search-match", new[] { Channel.InApp, Channel.Email },
            $"{e.MessageId}", new LocalizedText($"وظيفة جديدة تطابق بحثك: {e.JobTitle}", $"A new job matches your saved search: {e.JobTitle}"),
            new LocalizedText("تم نشر وظيفة جديدة تطابق بحثك المحفوظ.", "A new job matching your saved search was posted."), $"/jobs/{e.JobPostingId}"), ct);
}

/// <summary>JobRecommendationComputed (BC-10): the weekly recommendation, once per user and ISO week (US-3.6.2-03).</summary>
public sealed class SendWeeklyRecommendationHandler(NotificationComposer composer, INotificationPreferenceRepository preferences, IWeeklyCycleRepository cycles, TimeProvider clock)
    : IIntegrationEventHandler<JobRecommendationComputedIntegrationEvent>
{
    public async Task Handle(JobRecommendationComputedIntegrationEvent e, CancellationToken ct)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var recipient = e.ActorId; // the job seeker's account: recommendations are computed on behalf of the account
        var preference = await preferences.GetAsync(recipient, ct) ?? NotificationPreference.Default(recipient, now);
        if (!preference.InAppAllowed(Categories.WeeklyRecommendation) && !preference.EmailAllowed(Categories.WeeklyRecommendation))
        {
            return; // AC-02: the category is disabled, nothing is sent and the week stays open
        }

        var week = DedupeKeyFactory.IsoWeek(now);
        if (await cycles.ExistsAsync(recipient, week, ct))
        {
            return; // AC-03: already notified this week
        }

        cycles.Add(WeeklyCycle.For(recipient, now));
        await composer.ComposeAsync(new ComposeRequest(recipient, Categories.WeeklyRecommendation, "weekly-recommendation", new[] { Channel.InApp, Channel.Email },
            DedupeKeyFactory.Weekly(recipient, now), new LocalizedText("وظائف موصى بها لك هذا الأسبوع", "Recommended jobs for you this week"),
            new LocalizedText($"لدينا {e.TopJobIds.Count} وظائف موصى بها.", $"We found {e.TopJobIds.Count} recommended jobs for you."), "/jobs/recommendations"), ct);
    }
}

/// <summary>JobDataImported (BC-02): confirm the platform job id to the partner once; a re-push returns the same id (US-3.1.3-08).</summary>
public sealed class SendJobConfirmationHandler(IJobConfirmationRepository confirmations, NotificationComposer composer, TimeProvider clock)
    : IIntegrationEventHandler<JobDataImportedIntegrationEvent>
{
    public async Task Handle(JobDataImportedIntegrationEvent e, CancellationToken ct)
    {
        if (await confirmations.GetAsync(e.SourcePlatformId, e.SourceJobId, ct) is not null)
        {
            return;
        }

        confirmations.Add(JobConfirmation.Confirm(e.SourcePlatformId, e.SourceJobId, e.PlatformJobId, e.ActorId, clock.GetUtcNow().UtcDateTime));
        await composer.ComposeAsync(new ComposeRequest(e.ActorId, Categories.JobConfirmation, "job-confirmation", new[] { Channel.Email },
            $"confirm:{e.SourcePlatformId}:{e.SourceJobId}", new LocalizedText("تأكيد استلام الوظيفة", $"Job received: {e.PlatformJobId}"),
            new LocalizedText($"تم استلام وظيفتك ورقمها في المنصة {e.PlatformJobId}.", $"Your job {e.SourceJobId} was received. Platform job id: {e.PlatformJobId}."),
            null, "job-confirmation", new Dictionary<string, string> { ["platformJobId"] = e.PlatformJobId, ["sourceJobId"] = e.SourceJobId }), ct);
    }
}

/// <summary>AccountApproved (BC-03): welcome the newly approved user (e-mail and in-app; contact data is fetched at send time).</summary>
public sealed class SendWelcomeHandler(NotificationComposer composer) : IIntegrationEventHandler<AccountApprovedIntegrationEvent>
{
    public Task Handle(AccountApprovedIntegrationEvent e, CancellationToken ct) =>
        composer.ComposeAsync(new ComposeRequest(e.AccountId, Categories.Welcome, "welcome", new[] { Channel.InApp, Channel.Email }, $"{e.MessageId}",
            new LocalizedText("مرحبا بك في المنصة", "Welcome to JobPlatform"), new LocalizedText("تم تفعيل حسابك بنجاح.", "Your account is now active."),
            null, "welcome", new Dictionary<string, string>()), ct);
}

/// <summary>AccountSuspended (BC-03): stop notifying a deactivated user.</summary>
public sealed class SuppressRecipientHandler(INotificationPreferenceRepository preferences, TimeProvider clock) : IIntegrationEventHandler<AccountSuspendedIntegrationEvent>
{
    public async Task Handle(AccountSuspendedIntegrationEvent e, CancellationToken ct)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var preference = await preferences.GetAsync(e.AccountId, ct);
        if (preference is null)
        {
            preference = NotificationPreference.Default(e.AccountId, now);
            preferences.Add(preference);
        }

        preference.Suspend(now);
    }
}
