using JobPlatform.Notification.Application.Commands.Delivery;
using JobPlatform.Notification.Application.DTOs.Delivery;
using JobPlatform.Notification.Application.Services.Delivery;
using JobPlatform.Notification.Domain;
using JobPlatform.SharedKernel.Application.Abstractions;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.Notification.Application.Handlers.Delivery;

internal sealed class StartSendingHandler : ICommandHandler<StartSendingCommand, SendSpec>
{
    private readonly IOutboundMessageRepository _messages;

    public StartSendingHandler(IOutboundMessageRepository messages) => _messages = messages;

    public async Task<Result<SendSpec>> Handle(StartSendingCommand request, CancellationToken ct)
    {
        var message = await _messages.GetAsync(request.MessageId, ct);
        if (message is null)
        {
            return DeliveryService.NotFound;
        }

        message.BeginSending();
        return new SendSpec(message.Id, message.Channel, message.RecipientAccountId, message.Category, message.Subject, message.Body ?? string.Empty);
    }
}
