using System.Globalization;
using JobPlatform.SharedKernel.Domain;

namespace JobPlatform.Notification.Domain;

/// <summary>What the router decided for one channel: send it, or suppress it with a reason (kept for audit, never sent).</summary>
public sealed record ChannelDecision(Channel Channel, bool Send, string? Reason = null, bool AsDigest = false);

/// <summary>
/// Preference-to-channel routing (handover 3.8): honours consent and unsubscribes, keeps mandatory categories flowing, sends SMS only for essential
/// categories (3.6.3-03) and re-routes a non-essential SMS to e-mail and in-app.
/// </summary>
public static class ChannelRouter
{
    public const string Preference = "PREFERENCE_DISABLED";
    public const string Unsubscribed = "NT.Email.UNSUBSCRIBED";
    public const string NonEssentialRerouted = "NT.Sms.NON_ESSENTIAL_REROUTED";
    public const string AccountSuspended = "ACCOUNT_SUSPENDED";

    public static IReadOnlyList<ChannelDecision> Route(string category, IReadOnlyCollection<Channel> requested, NotificationPreference preference, SmsPolicy smsPolicy)
    {
        var channels = new List<Channel>(requested);
        var decisions = new List<ChannelDecision>();
        if (preference.Suspended)
        {
            return channels.Select(c => new ChannelDecision(c, false, AccountSuspended)).ToList();
        }

        // Index loop on purpose: a re-routed SMS appends e-mail and in-app to the list, and those are routed in the same pass.
        for (var i = 0; i < channels.Count; i++)
        {
            var channel = channels[i];
            if (decisions.Any(d => d.Channel == channel))
            {
                continue;
            }

            switch (channel)
            {
                case Channel.Sms when smsPolicy.IsEssential(category):
                    decisions.Add(new ChannelDecision(Channel.Sms, true));
                    break;
                case Channel.Sms:
                    decisions.Add(new ChannelDecision(Channel.Sms, false, NonEssentialRerouted));
                    foreach (var fallback in new[] { Channel.Email, Channel.InApp }.Where(f => !channels.Contains(f)))
                    {
                        channels.Add(fallback);
                    }

                    break;
                case Channel.Email:
                    decisions.Add(EmailDecision(category, preference));
                    break;
                case Channel.InApp:
                    decisions.Add(preference.InAppAllowed(category) ? new ChannelDecision(Channel.InApp, true) : new ChannelDecision(Channel.InApp, false, Preference));
                    break;
            }
        }

        return decisions.OrderBy(d => d.Channel).ToList();
    }

    private static ChannelDecision EmailDecision(string category, NotificationPreference preference)
    {
        if (preference.EmailAllowed(category))
        {
            var digest = preference.EmailMode == DeliveryMode.Digest && !Categories.IsMandatory(category);
            return new ChannelDecision(Channel.Email, true, null, digest);
        }

        return new ChannelDecision(Channel.Email, false, preference.IsUnsubscribed(category) ? Unsubscribed : Preference);
    }
}

/// <summary>Anti-spam (3.6.1-05) and telecom (3.6.3-06) controls checked before anything is handed to a provider.</summary>
public static class ComplianceGuard
{
    public const int MaxSmsSegments = 6;

    private static readonly string[] Prohibited = { "casino", "gambling", "lottery", "loan shark", "adult content", "قمار", "يانصيب" };

    public static void CheckEmail(string category, string body, bool senderDomainVerified)
    {
        Guard.Ensure(senderDomainVerified, NotificationRuleCodes.ComplianceViolation, "The sender domain is not verified.", null, BusinessRuleKind.BusinessRule);
        Guard.Ensure(!Categories.Marketing.Contains(category) || body.Contains("/unsubscribe/", StringComparison.OrdinalIgnoreCase), NotificationRuleCodes.ComplianceViolation,
            "Marketing and digest e-mail must carry an unsubscribe link.", null, BusinessRuleKind.BusinessRule);
    }

    public static void CheckSms(string body, string? senderId, IReadOnlyCollection<string> approvedSenderIds)
    {
        Guard.Ensure(!string.IsNullOrWhiteSpace(senderId) && approvedSenderIds.Contains(senderId), NotificationRuleCodes.ComplianceViolation, "The SMS sender id is not approved.", null,
            BusinessRuleKind.BusinessRule);
        Guard.Ensure(Segments(body) <= MaxSmsSegments, NotificationRuleCodes.ComplianceViolation, "The SMS exceeds the carrier segment limit.", NotificationErrorCodes.SmsInvalidField,
            BusinessRuleKind.InvalidInput);
        Guard.Ensure(!Prohibited.Any(p => body.Contains(p, StringComparison.OrdinalIgnoreCase)), NotificationRuleCodes.ComplianceViolation, "The SMS contains restricted content.",
            NotificationErrorCodes.SmsInvalidField, BusinessRuleKind.InvalidInput);
    }

    /// <summary>Carrier segments: GSM-7 text 160 characters (153 when concatenated), anything with non-ASCII such as Arabic is UCS-2 with 70 (67).</summary>
    public static int Segments(string body)
    {
        if (body.Length == 0)
        {
            return 0;
        }

        var unicode = body.Any(c => c > 127);
        var (single, multi) = unicode ? (70, 67) : (160, 153);
        return body.Length <= single ? 1 : (int)Math.Ceiling(body.Length / (double)multi);
    }
}

public static class DedupeKeyFactory
{
    public static string For(string sourceMessageId, Guid recipient, string category, Channel channel) => $"{sourceMessageId}:{recipient:N}:{category}:{channel}";

    public static string IsoWeek(DateTime utc) => $"{ISOWeek.GetYear(utc):D4}-W{ISOWeek.GetWeekOfYear(utc):D2}";

    public static string Weekly(Guid recipient, DateTime utc) => $"weekly:{recipient:N}:{IsoWeek(utc)}";
}

public sealed record JobConfirmationSentDomainEvent(DateTime At, Guid JobConfirmationId, Guid PartnerAccountId, Guid SourcePlatformId, string PlatformJobId) : DomainEvent(At);

/// <summary>Maps (source platform, source job) to the platform job id exactly once: a re-push returns the same id and never confirms twice (US-3.1.3-08 AC-02).</summary>
public sealed class JobConfirmation : AggregateRoot<Guid>
{
    private JobConfirmation()
    {
    }

    public Guid SourcePlatformId { get; private set; }
    public string SourceJobId { get; private set; } = string.Empty;
    public string PlatformJobId { get; private set; } = string.Empty;
    public Guid PartnerAccountId { get; private set; }
    public DateTime ConfirmedAtUtc { get; private set; }

    public static JobConfirmation Confirm(Guid sourcePlatformId, string sourceJobId, string platformJobId, Guid partnerAccountId, DateTime nowUtc)
    {
        Guard.Ensure(sourcePlatformId != Guid.Empty && !string.IsNullOrWhiteSpace(sourceJobId) && !string.IsNullOrWhiteSpace(platformJobId), NotificationRuleCodes.InvalidConfirmation,
            "A confirmation needs the source platform, the source job and the platform job id.");
        var confirmation = new JobConfirmation
        {
            Id = Guid.NewGuid(),
            SourcePlatformId = sourcePlatformId,
            SourceJobId = sourceJobId,
            PlatformJobId = platformJobId,
            PartnerAccountId = partnerAccountId,
            ConfirmedAtUtc = nowUtc
        };
        confirmation.Raise(new JobConfirmationSentDomainEvent(nowUtc, confirmation.Id, partnerAccountId, sourcePlatformId, platformJobId));
        return confirmation;
    }
}

/// <summary>One weekly recommendation per user and ISO week (US-3.6.2-03 AC-03).</summary>
public sealed class WeeklyCycle : Entity<Guid>
{
    private WeeklyCycle()
    {
    }

    public Guid AccountId { get; private set; }
    public string IsoWeek { get; private set; } = string.Empty;
    public DateTime CreatedAtUtc { get; private set; }

    public static WeeklyCycle For(Guid accountId, DateTime nowUtc) =>
        new() { Id = Guid.NewGuid(), AccountId = accountId, IsoWeek = DedupeKeyFactory.IsoWeek(nowUtc), CreatedAtUtc = nowUtc };
}
