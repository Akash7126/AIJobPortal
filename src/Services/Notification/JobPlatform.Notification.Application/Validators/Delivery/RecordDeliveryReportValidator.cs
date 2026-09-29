using FluentValidation;
using JobPlatform.Notification.Application.Commands.Delivery;

namespace JobPlatform.Notification.Application.Validators.Delivery;

public sealed class RecordDeliveryReportValidator : AbstractValidator<RecordDeliveryReportCommand>
{
    public RecordDeliveryReportValidator()
    {
        RuleFor(x => x.ProviderMessageId).NotEmpty().MaximumLength(200).WithErrorCode("VAL.ProviderMessageId.Required");
        RuleFor(x => x.Status).Must(s => s.ToLowerInvariant() is "delivered" or "failed").WithErrorCode("VAL.Status.Invalid");
    }
}
