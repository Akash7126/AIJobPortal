using JobPlatform.BuildingBlocks.Infrastructure.Persistence;
using JobPlatform.Notification.Application.Events;
using JobPlatform.Notification.Domain;
using JobPlatform.Notification.Infrastructure.Persistence;
using JobPlatform.Notification.Infrastructure.Persistence.Repositories;
using JobPlatform.SharedKernel.Application.Persistence;
using JobPlatform.SharedKernel.Common.ValueObjects;
using JobPlatform.TestSupport;
using Microsoft.EntityFrameworkCore;

namespace JobPlatform.Notification.Infrastructure.IntegrationTests;

internal static class Db
{
    public static readonly DateTime T0 = new(2026, 4, 1, 9, 0, 0, DateTimeKind.Utc);

    public static SqliteTestDatabase<NotificationDbContext> New() => new(o => new NotificationDbContext(o), new NotificationEventMapper());
}

public class InAppNotificationPersistenceTests
{
    [Fact]
    public async Task InAppNotification_RoundTrips_AndSavingWritesAnOutboxRow()
    {
        await using var database = Db.New();
        var recipient = Guid.NewGuid();
        var notification = InAppNotification.Create(recipient, "generic", Categories.Welcome, new LocalizedText("مرحبا", "Welcome"), new LocalizedText("أهلا", "Hello"),
            "/jobs", Db.T0);
        var id = notification.Id;

        await using (var write = database.NewContext())
        {
            new InAppNotificationRepository(write).Add(notification);
            await write.SaveChangesAsync();

            var outbox = await write.Set<OutboxMessage>().SingleAsync();
            outbox.Type.Should().Be("NotificationSent");
        }

        await using var read = database.NewContext();
        var loaded = await new InAppNotificationRepository(read).GetAsync(id);
        loaded!.Title.En.Should().Be("Welcome");
        loaded.Body.Ar.Should().Be("أهلا");
        loaded.Status.Should().Be(InAppStatus.Unread);
    }

    [Fact]
    public async Task InAppNotification_MarkRead_ThenReload_PersistsTheNewStatus()
    {
        await using var database = Db.New();
        var recipient = Guid.NewGuid();
        var notification = InAppNotification.Create(recipient, "generic", Categories.Welcome, new LocalizedText("A", "A"), new LocalizedText("B", "B"), null, Db.T0);
        var id = notification.Id;

        await using (var write = database.NewContext())
        {
            new InAppNotificationRepository(write).Add(notification);
            await write.SaveChangesAsync();
        }

        await using (var mark = database.NewContext())
        {
            var loaded = await new InAppNotificationRepository(mark).GetAsync(id);
            loaded!.MarkRead(new Actor(recipient, false), Db.T0.AddMinutes(5));
            await mark.SaveChangesAsync();
        }

        await using var read = database.NewContext();
        var final = await new InAppNotificationRepository(read).GetAsync(id);
        final!.Status.Should().Be(InAppStatus.Read);
        final.ReadAtUtc.Should().Be(Db.T0.AddMinutes(5));
    }
}

public class OutboundMessagePersistenceTests
{
    [Fact]
    public async Task OutboundMessage_RoundTrips_AndDedupeKeyIsUnique()
    {
        await using var database = Db.New();
        var recipient = Guid.NewGuid();
        var dedupeKey = DedupeKeyFactory.For(Guid.NewGuid().ToString(), recipient, Categories.Welcome, Channel.Email);
        await using (var write = database.NewContext())
        {
            new OutboundMessageRepository(write).Add(OutboundMessage.Compose(Channel.Email, recipient, Categories.Welcome, dedupeKey, "Welcome", "Hello there", "en", null, null,
                false, false, Db.T0));
            await write.SaveChangesAsync();
        }

        await using var read = database.NewContext();
        (await new OutboundMessageRepository(read).ExistsByDedupeKeyAsync(dedupeKey)).Should().BeTrue();

        await using var dup = database.NewContext();
        new OutboundMessageRepository(dup).Add(OutboundMessage.Compose(Channel.Sms, recipient, Categories.Welcome, dedupeKey, "Welcome", "Hi", "en", null, null, false, false,
            Db.T0));
        var act = () => dup.SaveChangesAsync();
        await act.Should().ThrowAsync<UniqueConstraintViolationException>();
    }

    [Fact]
    public async Task OutboundMessage_SendLifecycle_RoundTrips_AndRaisesAnOutboxRowOnSent()
    {
        await using var database = Db.New();
        var recipient = Guid.NewGuid();
        var message = OutboundMessage.Compose(Channel.Email, recipient, Categories.Welcome, Guid.NewGuid().ToString("N"), "Welcome", "Hello", "en", null, null, false, false, Db.T0);
        var id = message.Id;

        await using (var write = database.NewContext())
        {
            new OutboundMessageRepository(write).Add(message);
            await write.SaveChangesAsync();
        }

        await using (var send = database.NewContext())
        {
            var loaded = await new OutboundMessageRepository(send).GetAsync(id);
            loaded!.BeginSending();
            loaded.MarkSent("provider-msg-1", "j***@example.com", "JobPlatform", Db.T0.AddSeconds(2));
            await send.SaveChangesAsync();

            var outbox = await send.Set<OutboxMessage>().SingleAsync();
            outbox.Type.Should().Be("NotificationSent");
        }

        await using var read = database.NewContext();
        var final = await new OutboundMessageRepository(read).GetByProviderMessageIdAsync("provider-msg-1");
        final!.Status.Should().Be(MessageStatus.Sent);
        final.Id.Should().Be(id);
    }

    [Fact]
    public async Task ListDueAsync_ReturnsOnlyPendingNonDigestMessagesAtOrBeforeNow()
    {
        await using var database = Db.New();
        var recipient = Guid.NewGuid();
        await using (var write = database.NewContext())
        {
            var repo = new OutboundMessageRepository(write);
            repo.Add(OutboundMessage.Compose(Channel.Email, recipient, Categories.Welcome, "due-1", "S", "B", "en", null, null, false, false, Db.T0));
            repo.Add(OutboundMessage.Compose(Channel.Email, recipient, Categories.Welcome, "digest-1", "S", "B", "en", null, null, true, false, Db.T0));
            await write.SaveChangesAsync();
        }

        await using var read = database.NewContext();
        var due = await new OutboundMessageRepository(read).ListDueAsync(Db.T0.AddMinutes(1), take: 100);

        due.Should().ContainSingle().Which.DedupeKey.Should().Be("due-1");
    }

    [Fact]
    public async Task ListDigestCandidatesAsync_ReturnsOnlyDigestCandidatesCreatedBeforeTheCutoff()
    {
        await using var database = Db.New();
        var recipient = Guid.NewGuid();
        await using (var write = database.NewContext())
        {
            var repo = new OutboundMessageRepository(write);
            repo.Add(OutboundMessage.Compose(Channel.Email, recipient, Categories.WeeklyRecommendation, "digest-old", "S", "B", "en", null, null, true, false, Db.T0));
            repo.Add(OutboundMessage.Compose(Channel.Email, recipient, Categories.WeeklyRecommendation, "digest-new", "S", "B", "en", null, null, true, false, Db.T0.AddHours(23)));
            await write.SaveChangesAsync();
        }

        await using var read = database.NewContext();
        var candidates = await new OutboundMessageRepository(read).ListDigestCandidatesAsync(Db.T0.AddHours(1));

        candidates.Should().ContainSingle().Which.DedupeKey.Should().Be("digest-old");
    }

    [Fact]
    public async Task Suppressed_RoundTrips_WithTheReason_AndNoBody()
    {
        await using var database = Db.New();
        var recipient = Guid.NewGuid();
        var id = Guid.Empty;
        await using (var write = database.NewContext())
        {
            var message = OutboundMessage.Suppressed(Channel.Sms, recipient, Categories.News, "suppressed-1", ChannelRouter.Preference, Db.T0);
            id = message.Id;
            new OutboundMessageRepository(write).Add(message);
            await write.SaveChangesAsync();
        }

        await using var read = database.NewContext();
        var loaded = await new OutboundMessageRepository(read).GetAsync(id);
        loaded!.Status.Should().Be(MessageStatus.Suppressed);
        loaded.SuppressionReason.Should().Be(ChannelRouter.Preference);
        loaded.Body.Should().BeNull();
    }
}

public class NotificationPreferencePersistenceTests
{
    [Fact]
    public async Task NotificationPreference_RoundTrips_TheJsonBackedCollections()
    {
        await using var database = Db.New();
        var account = Guid.NewGuid();
        await using (var write = database.NewContext())
        {
            var preference = NotificationPreference.Default(account, Db.T0);
            preference.SetEmailPreference(new Actor(account, false), new Dictionary<string, bool> { [Categories.News] = false, [Categories.SavedSearchMatch] = true },
                DeliveryMode.Digest, Db.T0);
            preference.SetSmsOptIn(new Actor(account, false), "+970599000000", true, Db.T0);
            preference.Unsubscribe(Categories.SavedSearchMatch, Db.T0.AddMinutes(1));
            new NotificationPreferenceRepository(write).Add(preference);
            await write.SaveChangesAsync();
        }

        await using var read = database.NewContext();
        var loaded = await new NotificationPreferenceRepository(read).GetAsync(account);
        loaded!.EmailCategories[Categories.News].Should().BeFalse();
        loaded.EmailMode.Should().Be(DeliveryMode.Digest);
        loaded.SmsOptedIn.Should().BeTrue();
        // Unsubscribing after re-enabling in the same call: SetEmailPreference cleared any earlier unsubscribe of an enabled category,
        // but the later explicit Unsubscribe call still applies (it ran after Set).
        loaded.IsUnsubscribed(Categories.SavedSearchMatch).Should().BeTrue();
    }

    [Fact]
    public async Task NotificationPreference_Suspend_RoundTrips()
    {
        await using var database = Db.New();
        var account = Guid.NewGuid();
        await using (var write = database.NewContext())
        {
            var preference = NotificationPreference.Default(account, Db.T0);
            preference.Suspend(Db.T0);
            new NotificationPreferenceRepository(write).Add(preference);
            await write.SaveChangesAsync();
        }

        await using var read = database.NewContext();
        (await new NotificationPreferenceRepository(read).GetAsync(account))!.Suspended.Should().BeTrue();
    }
}

public class TemplateAndTypePersistenceTests
{
    [Fact]
    public async Task EmailTemplate_NewVersion_RoundTrips_AndCodeVersionLocaleIsUnique()
    {
        await using var database = Db.New();
        var placeholders = new Dictionary<string, string> { ["name"] = "there" };
        await using (var write = database.NewContext())
        {
            var v1 = EmailTemplate.Create("welcome", "en", "Welcome {{name}}", "Hi {{name}}", placeholders, Db.T0);
            new EmailTemplateRepository(write).Add(v1);
            await write.SaveChangesAsync();
        }

        await using (var write2 = database.NewContext())
        {
            var v1 = await new EmailTemplateRepository(write2).GetCurrentAsync("welcome", "en");
            var v2 = v1!.NewVersion(new Actor(Guid.NewGuid(), true), "Welcome back {{name}}", "Hi {{name}} again", placeholders, Db.T0.AddDays(1));
            new EmailTemplateRepository(write2).Add(v2);
            await write2.SaveChangesAsync();
        }

        await using var read = database.NewContext();
        var current = await new EmailTemplateRepository(read).GetCurrentAsync("welcome", "en");
        current!.Version.Should().Be(2);
        current.Subject.Should().Be("Welcome back {{name}}");
        (await new EmailTemplateRepository(read).GetVersionAsync("welcome", "en", 1))!.Subject.Should().Be("Welcome {{name}}");

        await using var dup = database.NewContext();
        new EmailTemplateRepository(dup).Add(EmailTemplate.Create("welcome", "en", "Dup", "Dup body", new Dictionary<string, string>(), Db.T0));
        // A fresh Create() always starts at version 1, colliding with the existing version-1 row for the same code+locale.
        var act = () => dup.SaveChangesAsync();
        await act.Should().ThrowAsync<UniqueConstraintViolationException>();
    }

    [Fact]
    public async Task NotificationType_Define_ThenRedefine_RoundTrips()
    {
        await using var database = Db.New();
        var admin = new Actor(Guid.NewGuid(), true);
        await using (var write = database.NewContext())
        {
            new NotificationTypeRepository(write).Add(NotificationType.Define(admin, "job-match", "star", "#1D4ED8", "Job match", false));
            await write.SaveChangesAsync();
        }

        await using (var redefine = database.NewContext())
        {
            var repo = new NotificationTypeRepository(redefine);
            var type = await repo.GetAsync("job-match");
            type!.Redefine(admin, "briefcase", "#1D4ED8", "Job match updated", true);
            await redefine.SaveChangesAsync();
        }

        await using var read = database.NewContext();
        var loaded = await new NotificationTypeRepository(read).GetAsync("job-match");
        loaded!.Icon.Should().Be("briefcase");
        loaded.IsMandatory.Should().BeTrue();
        (await new NotificationTypeRepository(read).ListAsync()).Should().Contain(t => t.Id == "job-match");
    }

    [Fact]
    public async Task SmsPolicy_NewVersion_RoundTrips_TheLatestVersion()
    {
        await using var database = Db.New();
        var admin = new Actor(Guid.NewGuid(), true);
        await using (var write = database.NewContext())
        {
            var initial = SmsPolicy.Initial(Db.T0);
            new SmsPolicyRepository(write).Add(initial);
            await write.SaveChangesAsync();
        }

        await using (var next = database.NewContext())
        {
            var repo = new SmsPolicyRepository(next);
            var current = await repo.GetCurrentAsync();
            var updated = current.NewVersion(admin, new[] { Categories.Otp, Categories.PasswordReset, Categories.SecurityAlert }, Db.T0.AddDays(1));
            repo.Add(updated);
            await next.SaveChangesAsync();
        }

        await using var read = database.NewContext();
        var latest = await new SmsPolicyRepository(read).GetCurrentAsync();
        latest.EssentialCategories.Should().Contain(Categories.SecurityAlert);
        latest.IsEssential(Categories.SecurityAlert).Should().BeTrue();
    }
}

public class ConfirmationAndCyclePersistenceTests
{
    [Fact]
    public async Task JobConfirmation_Confirm_RoundTrips_AndSourceIsUnique_WithOutboxRow()
    {
        await using var database = Db.New();
        var sourcePlatformId = Guid.NewGuid();
        var partner = Guid.NewGuid();
        await using (var write = database.NewContext())
        {
            new JobConfirmationRepository(write).Add(JobConfirmation.Confirm(sourcePlatformId, "ext-job-1", "platform-job-1", partner, Db.T0));
            await write.SaveChangesAsync();

            var outbox = await write.Set<OutboxMessage>().SingleAsync();
            outbox.Type.Should().Be("JobConfirmationSent");
        }

        await using var read = database.NewContext();
        var loaded = await new JobConfirmationRepository(read).GetAsync(sourcePlatformId, "ext-job-1");
        loaded!.PlatformJobId.Should().Be("platform-job-1");

        await using var dup = database.NewContext();
        new JobConfirmationRepository(dup).Add(JobConfirmation.Confirm(sourcePlatformId, "ext-job-1", "platform-job-2", partner, Db.T0));
        var act = () => dup.SaveChangesAsync();
        await act.Should().ThrowAsync<UniqueConstraintViolationException>();
    }

    [Fact]
    public async Task WeeklyCycle_ExistsAsync_EnforcesOnePerAccountAndWeek()
    {
        await using var database = Db.New();
        var account = Guid.NewGuid();
        await using (var write = database.NewContext())
        {
            new WeeklyCycleRepository(write).Add(WeeklyCycle.For(account, Db.T0));
            await write.SaveChangesAsync();
        }

        await using var read = database.NewContext();
        (await new WeeklyCycleRepository(read).ExistsAsync(account, DedupeKeyFactory.IsoWeek(Db.T0))).Should().BeTrue();

        await using var dup = database.NewContext();
        new WeeklyCycleRepository(dup).Add(WeeklyCycle.For(account, Db.T0.AddHours(1)));
        var act = () => dup.SaveChangesAsync();
        await act.Should().ThrowAsync<UniqueConstraintViolationException>();
    }
}
