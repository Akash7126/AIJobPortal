using FluentValidation;
using JobPlatform.AuditLogging.Application.Queries.AuditLog;
using JobPlatform.AuditLogging.Application.Validators.Common;

namespace JobPlatform.AuditLogging.Application.Validators.AuditLog;

public sealed class ListNotificationHistoryQueryValidator : AbstractValidator<ListNotificationHistoryQuery>
{
    public ListNotificationHistoryQueryValidator()
    {
        RuleFor(x => x.Page).ValidPage();
        RuleFor(x => x.PageSize).ValidPageSize();
    }
}
