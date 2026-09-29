using JobPlatform.SharedKernel.Application.Abstractions;

namespace JobPlatform.Notification.Application.Commands.Delivery;

public sealed record RecordDeliveryReportCommand(string ProviderMessageId, string Status) : ICommand<bool>;
