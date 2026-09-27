using JobPlatform.Notification.Domain;
using JobPlatform.SharedKernel.Common.ValueObjects;
using JobPlatform.SharedKernel.Domain;
using Microsoft.Extensions.Options;

namespace JobPlatform.Notification.Application.Composition;

/// <summary>What a use case wants to tell one user. The composer decides the channels, renders, applies the compliance controls and creates the messages.</summary>
public sealed record ComposeRequest(
    Guid Recipient,
    string Category,
    string TypeCode,
    IReadOnlyCollection<Channel> Channels,
    string SourceMessageId,
    LocalizedText Title,
    LocalizedText Body,
    string? ActionUrl = null,
    string? TemplateCode = null,
    IReadOnlyDictionary<string, string>? TemplateValues = null,
    bool ContainsSecret = false);

public sealed record ComposeResult(IReadOnlyList<Channel> Created, IReadOnlyList<(Channel Channel, string Reason)> Suppressed, bool WasDuplicate);

/// <summary>
/// Channel routing, idempotent composition (INV-06: the same source message and recipient never yield a second e-mail or SMS), template rendering with
/// default placeholders, and the anti-spam / telecom controls. Handlers call this instead of writing tables (foundation 9.4).
/// </summary>
public sealed class NotificationComposer
{
    private readonly INotificationPreferenceRepository _preferences;
    private readonly ISmsPolicyRepository _smsPolicy;
    private readonly IOutboundMessageRepository _messages;
    private readonly IInAppNotificationRepository _inApp;
    private readonly IEmailTemplateRepository _templates;
    private readonly NotificationOptions _options;
    private readonly IUnsubscribeTokens _tokens;
    private readonly TimeProvider _clock;

    public NotificationComposer(INotificationPreferenceRepository preferences, ISmsPolicyRepository smsPolicy, IOutboundMessageRepository messages,
        IInAppNotificationRepository inApp, IEmailTemplateRepository templates, IOptions<NotificationOptions> options, IUnsubscribeTokens tokens, TimeProvider clock)
    {
        _preferences = preferences;
        _smsPolicy = smsPolicy;
        _messages = messages;
        _inApp = inApp;
        _templates = templates;
        _options = options.Value;
        _tokens = tokens;
        _clock = clock;
    }

    public async Task<ComposeResult> ComposeAsync(ComposeRequest request, CancellationToken ct)
    {
        var now = _clock.GetUtcNow().UtcDateTime;
        var category = Categories.Canonical(request.Category);
        var preference = await _preferences.GetAsync(request.Recipient, ct) ?? NotificationPreference.Default(request.Recipient, now);
        var policy = await _smsPolicy.GetCurrentAsync(ct);
        var decisions = ChannelRouter.Route(category, request.Channels, preference, policy);

        var created = new List<Channel>();
        var suppressed = new List<(Channel, string)>();
        var duplicate = false;
        foreach (var decision in decisions)
        {
            if (decision.Channel == Channel.InApp)
            {
                if (decision.Send)
                {
                    _inApp.Add(InAppNotification.Create(request.Recipient, request.TypeCode, category, request.Title, request.Body, request.ActionUrl, now));
                    created.Add(Channel.InApp);
                }
                else
                {
                    suppressed.Add((Channel.InApp, decision.Reason!));
                }

                continue;
            }

            var key = DedupeKeyFactory.For(request.SourceMessageId, request.Recipient, category, decision.Channel);
            if (await _messages.ExistsByDedupeKeyAsync(key, ct))
            {
                duplicate = true; // NT.Message.DUPLICATE: the same event never yields a second message
                continue;
            }

            if (!decision.Send)
            {
                _messages.Add(OutboundMessage.Suppressed(decision.Channel, request.Recipient, category, key, decision.Reason!, now));
                suppressed.Add((decision.Channel, decision.Reason!));
                continue;
            }

            var message = await BuildAsync(decision, request, category, key, now, ct);
            _messages.Add(message);
            if (message.Status == MessageStatus.Suppressed)
            {
                suppressed.Add((decision.Channel, message.SuppressionReason!));
            }
            else
            {
                created.Add(decision.Channel);
            }
        }

        return new ComposeResult(created, suppressed, duplicate);
    }

    private async Task<OutboundMessage> BuildAsync(ChannelDecision decision, ComposeRequest request, string category, string key, DateTime now, CancellationToken ct)
    {
        var locale = _options.DefaultLocale;
        try
        {
            if (decision.Channel == Channel.Email)
            {
                var (subject, body, code, version) = await RenderEmailAsync(request, category, locale, ct);
                ComplianceGuard.CheckEmail(category, body, _options.SenderDomainVerified);
                return OutboundMessage.Compose(Channel.Email, request.Recipient, category, key, subject, body, locale, code, version, decision.AsDigest, false, now);
            }

            var text = request.Body.For(locale == "ar" ? SharedKernel.Common.Enums.Language.Ar : SharedKernel.Common.Enums.Language.En);
            ComplianceGuard.CheckSms(text, _options.SmsSenderId, _options.ApprovedSmsSenderIds);
            return OutboundMessage.Compose(Channel.Sms, request.Recipient, category, key, string.Empty, text, locale, null, null, false, request.ContainsSecret, now);
        }
        catch (BusinessRuleViolationException ex) when (ex.Code == NotificationRuleCodes.ComplianceViolation)
        {
            // A message that would break an anti-spam or telecom control is kept as suppressed, never sent.
            return OutboundMessage.Suppressed(decision.Channel, request.Recipient, category, key, ex.Code, now);
        }
    }

    private async Task<(string Subject, string Body, string? Code, int? Version)> RenderEmailAsync(ComposeRequest request, string category, string locale, CancellationToken ct)
    {
        var footer = Categories.Marketing.Contains(category)
            ? $"\n\nUnsubscribe: {_options.PublicBaseUrl.TrimEnd('/')}/unsubscribe/{_tokens.Create(request.Recipient, category)}"
            : string.Empty;
        if (request.TemplateCode is { } code && await _templates.GetCurrentAsync(code, locale, ct) is { } template)
        {
            var (subject, body) = template.Render(request.TemplateValues ?? new Dictionary<string, string>());
            return (subject, body + footer, template.Code, template.Version);
        }

        var isArabic = locale == "ar";
        return (request.Title.For(isArabic ? SharedKernel.Common.Enums.Language.Ar : SharedKernel.Common.Enums.Language.En),
            request.Body.For(isArabic ? SharedKernel.Common.Enums.Language.Ar : SharedKernel.Common.Enums.Language.En) + footer, null, null);
    }
}
