using FluentValidation;
using JobPlatform.Notification.Application.Queries.Admin;
using JobPlatform.Notification.Application.Validators.Common;
using JobPlatform.Notification.Domain;

namespace JobPlatform.Notification.Application.Validators.Admin;

public sealed class ListSmsDeliveryStatusValidator : AbstractValidator<ListSmsDeliveryStatusQuery>
{
    public ListSmsDeliveryStatusValidator()
    {
        RuleFor(x => x.Page).ValidPage();
        RuleFor(x => x.PageSize).ValidPageSize();
        When(x => !string.IsNullOrEmpty(x.Status), () =>
            RuleFor(x => x.Status!).Must(s => Enum.TryParse<DeliveryStatus>(s, true, out _)).OverridePropertyName("Status").WithErrorCode("VAL.Status.Invalid"));
    }
}
