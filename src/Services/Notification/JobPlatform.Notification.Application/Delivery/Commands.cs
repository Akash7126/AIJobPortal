using FluentValidation;
using JobPlatform.Notification.Application.Composition;
using JobPlatform.Notification.Domain;
using JobPlatform.SharedKernel.Application.Abstractions;
using JobPlatform.SharedKernel.Application.Results;
using JobPlatform.SharedKernel.Common.ValueObjects;

namespace JobPlatform.Notification.Application.Delivery;

// ---------------------------------------------------------------------- worker steps (no DB transaction is held across the provider call)

public sealed record SendSpec(Guid MessageId, Channel Channel, Guid Recipient, string Category, string Subject, string Body);

public sealed record StartSendingCommand(Guid MessageId) : ICommand<SendSpec>;

public sealed record CompleteSendCommand(Guid MessageId, string ProviderMessageId, string MaskedRecipient, string? SenderIdentity) : ICommand;

public sealed record FailSendCommand(Guid MessageId, string ErrorCode) : ICommand;

// ---------------------------------------------------------------------- provider delivery report (US-3.6.3-04), signed webhook

public sealed record RecordDeliveryReportCommand(string ProviderMessageId, string Status) : ICommand<bool>;

public sealed class RecordDeliveryReportValidator : AbstractValidator<RecordDeliveryReportCommand>
{
    public RecordDeliveryReportValidator()
    {
        RuleFor(x => x.ProviderMessageId).NotEmpty().MaximumLength(200).WithErrorCode("VAL.ProviderMessageId.Required");
        RuleFor(x => x.Status).Must(s => s.ToLowerInvariant() is "delivered" or "failed").WithErrorCode("VAL.Status.Invalid");
    }
}

// ---------------------------------------------------------------------- transactional SMS for BC-03 (Q-04): OTP, password reset

/// <param name="Purpose">Otp or PasswordReset (both essential categories).</param>
public sealed record SendTransactionalSmsCommand(Guid AccountId, string Purpose, string Text, string? IdempotencyKey)
    : ServiceRequest, ICommand<TransactionalSmsResult>, IIdempotentCommand;

public sealed record TransactionalSmsResult(bool Accepted, string Status);

public sealed class SendTransactionalSmsValidator : AbstractValidator<SendTransactionalSmsCommand>
{
    public SendTransactionalSmsValidator()
    {
        RuleFor(x => x.AccountId).NotEmpty().WithErrorCode("VAL.AccountId.Required");
        RuleFor(x => x.Purpose).Must(p => p is Categories.Otp or Categories.PasswordReset).WithErrorCode("VAL.Purpose.Invalid");
        RuleFor(x => x.Text).NotEmpty().MaximumLength(500).WithErrorCode("VAL.Text.Invalid");
    }
}

public sealed record GetNotificationDetailQuery(Guid Id) : ServiceRequest, IQuery<NotificationDetailDto>;

internal sealed class DeliveryHandlers :
    ICommandHandler<StartSendingCommand, SendSpec>,
    ICommandHandler<CompleteSendCommand, Unit>,
    ICommandHandler<FailSendCommand, Unit>,
    ICommandHandler<RecordDeliveryReportCommand, bool>,
    ICommandHandler<SendTransactionalSmsCommand, TransactionalSmsResult>,
    IQueryHandler<GetNotificationDetailQuery, NotificationDetailDto>
{
    private readonly IOutboundMessageRepository _messages;
    private readonly INotificationReadStore _store;
    private readonly NotificationComposer _composer;
    private readonly TimeProvider _clock;

    public DeliveryHandlers(IOutboundMessageRepository messages, INotificationReadStore store, NotificationComposer composer, TimeProvider clock)
    {
        _messages = messages;
        _store = store;
        _composer = composer;
        _clock = clock;
    }

    private DateTime Now => _clock.GetUtcNow().UtcDateTime;

    private static Error NotFound => Error.NotFound(NotificationErrorCodes.NotificationNotFound, "The notification was not found.");

    public async Task<Result<SendSpec>> Handle(StartSendingCommand request, CancellationToken ct)
    {
        var message = await _messages.GetAsync(request.MessageId, ct);
        if (message is null)
        {
            return NotFound;
        }

        message.BeginSending();
        return new SendSpec(message.Id, message.Channel, message.RecipientAccountId, message.Category, message.Subject, message.Body ?? string.Empty);
    }

    public async Task<Result<Unit>> Handle(CompleteSendCommand request, CancellationToken ct)
    {
        var message = await _messages.GetAsync(request.MessageId, ct);
        if (message is null)
        {
            return NotFound;
        }

        message.MarkSent(request.ProviderMessageId, request.MaskedRecipient, request.SenderIdentity, Now);
        return Result.Success();
    }

    public async Task<Result<Unit>> Handle(FailSendCommand request, CancellationToken ct)
    {
        var message = await _messages.GetAsync(request.MessageId, ct);
        if (message is null)
        {
            return NotFound;
        }

        message.RecordFailure(request.ErrorCode, Now);
        return Result.Success();
    }

    /// <summary>Unknown provider ids are ignored (returns false) so a webhook retry storm never errors; Pending stays Pending until reported.</summary>
    public async Task<Result<bool>> Handle(RecordDeliveryReportCommand request, CancellationToken ct)
    {
        var message = await _messages.GetByProviderMessageIdAsync(request.ProviderMessageId, ct);
        return message is not null
               && message.RecordDeliveryReport(Enum.Parse<DeliveryStatus>(request.Status, true));
    }

    public async Task<Result<TransactionalSmsResult>> Handle(SendTransactionalSmsCommand request, CancellationToken ct)
    {
        var text = new LocalizedText(request.Text, request.Text);
        var result = await _composer.ComposeAsync(new ComposeRequest(request.AccountId, request.Purpose, request.Purpose.ToLowerInvariant(), new[] { Channel.Sms },
            request.IdempotencyKey ?? Guid.NewGuid().ToString("N"), text, text, ContainsSecret: true), ct);
        return new TransactionalSmsResult(result.Created.Contains(Channel.Sms), result.Created.Contains(Channel.Sms) ? "Queued" : result.WasDuplicate ? "Duplicate" : "Suppressed");
    }

    public async Task<Result<NotificationDetailDto>> Handle(GetNotificationDetailQuery request, CancellationToken ct) =>
        await _store.GetDetailAsync(request.Id, ct) is { } detail ? detail : NotFound;
}
