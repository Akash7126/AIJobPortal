using JobPlatform.Notification.Application.Commands.Delivery;
using JobPlatform.Notification.Domain;
using JobPlatform.Notification.Domain.Interfaces.Repositories;
using JobPlatform.SharedKernel.Application.Interfaces.Cqrs;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.Notification.Application.Handlers.Delivery;

internal sealed class RecordDeliveryReportHandler : ICommandHandler<RecordDeliveryReportCommand, bool>
{
    private readonly IOutboundMessageRepository _messages;

    public RecordDeliveryReportHandler(IOutboundMessageRepository messages) => _messages = messages;

    /// <summary>Unknown provider ids are ignored (returns false) so a webhook retry storm never errors; Pending stays Pending until reported.</summary>
    public async Task<Result<bool>> Handle(RecordDeliveryReportCommand request, CancellationToken ct)
    {
        var message = await _messages.GetByProviderMessageIdAsync(request.ProviderMessageId, ct);
        return message is not null
               && message.RecordDeliveryReport(Enum.Parse<DeliveryStatus>(request.Status, true));
    }
}
