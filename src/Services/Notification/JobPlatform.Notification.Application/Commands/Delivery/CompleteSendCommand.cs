using JobPlatform.SharedKernel.Application.Interfaces.Cqrs;

namespace JobPlatform.Notification.Application.Commands.Delivery;

public sealed record CompleteSendCommand(Guid MessageId, string ProviderMessageId, string MaskedRecipient, string? SenderIdentity) : ICommand;
