using JobPlatform.Notification.Application.Commands.Delivery;
using JobPlatform.Notification.Application.Services.Delivery;
using JobPlatform.Notification.Domain;
using JobPlatform.SharedKernel.Application.Abstractions;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.Notification.Application.Handlers.Delivery;

internal sealed class FailSendHandler : ICommandHandler<FailSendCommand, Unit>
{
    private readonly IOutboundMessageRepository _messages;
    private readonly DeliveryService _deliveryService;

    public FailSendHandler(IOutboundMessageRepository messages, DeliveryService deliveryService)
    {
        _messages = messages;
        _deliveryService = deliveryService;
    }

    public async Task<Result<Unit>> Handle(FailSendCommand request, CancellationToken ct)
    {
        var message = await _messages.GetAsync(request.MessageId, ct);
        if (message is null)
        {
            return DeliveryService.NotFound;
        }

        message.RecordFailure(request.ErrorCode, _deliveryService.Now);
        return Result.Success();
    }
}
