using FluentValidation;
using JobPlatform.AuditLogging.Application.Queries.AuditLog;
using JobPlatform.AuditLogging.Application.Validators.Common;

namespace JobPlatform.AuditLogging.Application.Validators.AuditLog;

public sealed class GetJobAuditTrailQueryValidator : FilteredListValidator<GetJobAuditTrailQuery>
{
    public GetJobAuditTrailQueryValidator() => RuleFor(x => x.PlatformJobId).NotEmpty().MaximumLength(128).WithErrorCode("VAL.PlatformJobId.Required");
}
