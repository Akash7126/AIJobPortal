using FluentValidation;
using JobPlatform.AuditLogging.Application.Queries.AuditLog;
using JobPlatform.AuditLogging.Application.Validators.Common;

namespace JobPlatform.AuditLogging.Application.Validators.AuditLog;

public sealed class ListSmsMessageLogQueryValidator : AbstractValidator<ListSmsMessageLogQuery>
{
    public ListSmsMessageLogQueryValidator()
    {
        RuleFor(x => x.Page).ValidPage();
        RuleFor(x => x.PageSize).ValidPageSize();
    }
}
