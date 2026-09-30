using JobPlatform.Notification.Application.Commands.Delivery;
using JobPlatform.Notification.Application.DTOs.Delivery;
using JobPlatform.Notification.Application.Interfaces;
using JobPlatform.Notification.Domain;
using JobPlatform.Notification.Domain.Interfaces.Repositories;
using JobPlatform.SharedKernel.Application.Interfaces.Cqrs;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace JobPlatform.Notification.Application.Delivery;

/// <summary>
/// Sends due e-mail and SMS: mark sending (committed), call the provider outside any transaction, then record the outcome. A provider timeout or
/// refusal goes back to Pending with backoff for 3 retries, then Failed with E-EMAILN-UPSTREAM-TIMEOUT / E-SMSN-UPSTREAM-TIMEOUT (A-02-003/004).
/// One message never blocks the next.
/// </summary>
public sealed class OutboundDispatcher
{
    private readonly IOutboundMessageRepository _messages;
    private readonly ISender _sender;
    private readonly IAccountContactApi _contacts;
    private readonly IEmailProvider _email;
    private readonly ISmsGateway _sms;
    private readonly IUnsubscribeTokens _tokens;
    private readonly NotificationOptions _options;
    private readonly TimeProvider _clock;
    private readonly ILogger<OutboundDispatcher> _logger;

    public OutboundDispatcher(IOutboundMessageRepository messages, ISender sender, IAccountContactApi contacts, IEmailProvider email, ISmsGateway sms, IUnsubscribeTokens tokens,
        IOptions<NotificationOptions> options, TimeProvider clock, ILogger<OutboundDispatcher> logger)
    {
        _messages = messages;
        _sender = sender;
        _contacts = contacts;
        _email = email;
        _sms = sms;
        _tokens = tokens;
        _options = options.Value;
        _clock = clock;
        _logger = logger;
    }

    /// <summary>Processes up to <paramref name="take"/> due messages; returns how many were attempted.</summary>
    public async Task<int> RunOnceAsync(int take, CancellationToken ct)
    {
        var due = await _messages.ListDueAsync(_clock.GetUtcNow().UtcDateTime, take, ct);
        foreach (var due1 in due)
        {
            var command = new StartSendingCommand(due1.Id);
            var started = await _sender.Send(command, ct);
            if (started.IsFailure)
            {
                continue;
            }

            var spec = started.Value;
            try
            {
                var (result, masked, senderIdentity) = await DeliverAsync(spec, ct);
                if (result.Accepted)
                {
                    var completeSendCommand = new CompleteSendCommand(spec.MessageId, result.ProviderMessageId!, masked, senderIdentity);
                    await _sender.Send(completeSendCommand, ct);
                }
                else
                {
                    var failSendCommand = new FailSendCommand(spec.MessageId, result.TimedOut ? OutboundMessage.TimeoutCode(spec.Channel) : result.Error ?? "E-NOTIF-PROVIDER-REFUSED");
                    await _sender.Send(failSendCommand, ct);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Sending message {MessageId} failed", spec.MessageId);
                var command2 = new FailSendCommand(spec.MessageId, OutboundMessage.TimeoutCode(spec.Channel));
                await _sender.Send(command2, ct);
            }
        }

        return due.Count;
    }

    private async Task<(ProviderResult Result, string Masked, string? Sender)> DeliverAsync(SendSpec spec, CancellationToken ct)
    {
        var contact = await _contacts.GetContactAsync(spec.Recipient, ct);
        if (spec.Channel == Channel.Email)
        {
            if (contact?.Email is not { } address)
            {
                return (ProviderResult.Fail(NotificationErrorCodes.ContactUnavailable), "-", null);
            }

            var unsubscribe = Categories.Marketing.Contains(spec.Category) ? $"{_options.PublicBaseUrl.TrimEnd('/')}/unsubscribe/{_tokens.Create(spec.Recipient, spec.Category)}" : null;
            var request = new EmailEnvelope(_options.EmailSenderAddress, address, spec.Subject, spec.Body, unsubscribe);
            var result = await _email.SendAsync(request, ct);
            return (result, Masking.Email(address), _options.EmailSenderAddress);
        }

        if (contact?.Mobile is not { } mobile)
        {
            return (ProviderResult.Fail(NotificationErrorCodes.ContactUnavailable), "-", null);
        }

        var smsEnvelope = new SmsEnvelope(_options.SmsSenderId, mobile, spec.Body);
        var sms = await _sms.SendAsync(smsEnvelope, ct);
        return (sms, Masking.Mobile(mobile), _options.SmsSenderId);
    }
}
