using JobPlatform.SharedKernel.Application.Abstractions;

namespace JobPlatform.Notification.Application.Commands.Delivery;

public sealed record CompleteSendCommand(Guid MessageId, string ProviderMessageId, string MaskedRecipient, string? SenderIdentity) : ICommand;
