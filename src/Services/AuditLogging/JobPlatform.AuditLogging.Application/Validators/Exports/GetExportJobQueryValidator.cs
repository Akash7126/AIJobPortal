using FluentValidation;
using JobPlatform.AuditLogging.Application.Queries.Exports;

namespace JobPlatform.AuditLogging.Application.Validators.Exports;

public sealed class GetExportJobQueryValidator : AbstractValidator<GetExportJobQuery>
{
    public GetExportJobQueryValidator() => RuleFor(x => x.Id).NotEmpty().WithErrorCode("VAL.Id.Required");
}
