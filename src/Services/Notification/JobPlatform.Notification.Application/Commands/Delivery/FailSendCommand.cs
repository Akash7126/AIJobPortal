using JobPlatform.SharedKernel.Application.Interfaces.Cqrs;

namespace JobPlatform.Notification.Application.Commands.Delivery;

public sealed record FailSendCommand(Guid MessageId, string ErrorCode) : ICommand;
