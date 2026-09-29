using FluentValidation;
using JobPlatform.Notification.Application.Queries.InApp;
using JobPlatform.Notification.Application.Validators.Common;
using JobPlatform.Notification.Domain;

namespace JobPlatform.Notification.Application.Validators.InApp;

public sealed class ListInAppNotificationsValidator : AbstractValidator<ListInAppNotificationsQuery>
{
    public ListInAppNotificationsValidator()
    {
        RuleFor(x => x.Page).ValidPage();
        RuleFor(x => x.PageSize).ValidPageSize();
        When(x => !string.IsNullOrEmpty(x.Status), () =>
            RuleFor(x => x.Status!).Must(s => Enum.TryParse<InAppStatus>(s, true, out var v) && v != InAppStatus.Deleted).OverridePropertyName("Status")
                .WithErrorCode("VAL.Status.Invalid"));
    }
}
