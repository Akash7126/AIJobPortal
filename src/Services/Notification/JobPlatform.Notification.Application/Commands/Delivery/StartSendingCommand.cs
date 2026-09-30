using JobPlatform.Notification.Application.DTOs.Delivery;
using JobPlatform.SharedKernel.Application.Interfaces.Cqrs;

namespace JobPlatform.Notification.Application.Commands.Delivery;

public sealed record StartSendingCommand(Guid MessageId) : ICommand<SendSpec>;
