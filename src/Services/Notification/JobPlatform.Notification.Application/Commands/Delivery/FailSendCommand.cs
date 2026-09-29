using JobPlatform.SharedKernel.Application.Abstractions;

namespace JobPlatform.Notification.Application.Commands.Delivery;

public sealed record FailSendCommand(Guid MessageId, string ErrorCode) : ICommand;
