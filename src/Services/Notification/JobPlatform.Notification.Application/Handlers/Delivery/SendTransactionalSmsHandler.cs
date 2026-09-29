using JobPlatform.Notification.Application.Commands.Delivery;
using JobPlatform.Notification.Application.Composition;
using JobPlatform.Notification.Application.DTOs.Delivery;
using JobPlatform.Notification.Domain;
using JobPlatform.SharedKernel.Application.Abstractions;
using JobPlatform.SharedKernel.Application.Results;
using JobPlatform.SharedKernel.Common.ValueObjects;

namespace JobPlatform.Notification.Application.Handlers.Delivery;

internal sealed class SendTransactionalSmsHandler : ICommandHandler<SendTransactionalSmsCommand, TransactionalSmsResult>
{
    private readonly NotificationComposer _composer;

    public SendTransactionalSmsHandler(NotificationComposer composer) => _composer = composer;

    public async Task<Result<TransactionalSmsResult>> Handle(SendTransactionalSmsCommand request, CancellationToken ct)
    {
        var text = new LocalizedText(request.Text, request.Text);
        var result = await _composer.ComposeAsync(new ComposeRequest(request.AccountId, request.Purpose, request.Purpose.ToLowerInvariant(), new[] { Channel.Sms },
            request.IdempotencyKey ?? Guid.NewGuid().ToString("N"), text, text, ContainsSecret: true), ct);
        return new TransactionalSmsResult(result.Created.Contains(Channel.Sms), result.Created.Contains(Channel.Sms) ? "Queued" : result.WasDuplicate ? "Duplicate" : "Suppressed");
    }
}
