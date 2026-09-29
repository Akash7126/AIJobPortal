using FluentValidation;
using JobPlatform.AuditLogging.Application.Queries.AuditLog;

namespace JobPlatform.AuditLogging.Application.Validators.AuditLog;

public sealed class GetJobStatusHistoryQueryValidator : AbstractValidator<GetJobStatusHistoryQuery>
{
    public GetJobStatusHistoryQueryValidator() => RuleFor(x => x.JobPostingId).NotEmpty().WithErrorCode("VAL.JobPostingId.Required");
}
