using FluentValidation;
using JobPlatform.Reporting.Application.Commands.ReportRuns;
using JobPlatform.Reporting.Domain;

namespace JobPlatform.Reporting.Application.Validators.ReportRuns;

/// <summary>BuildReportDefinitionValidator: at most 30 fields; the rest (whitelist, typed filters) is the definition's own validation.</summary>
public sealed class BuildReportDefinitionValidator : AbstractValidator<BuildReportDefinitionCommand>
{
    public BuildReportDefinitionValidator()
    {
        RuleFor(x => x.Definition).NotNull().WithErrorCode("VAL.Definition.Required");
        RuleFor(x => x.Definition.Fields).Must(f => f is null || f.Count <= ReportDefinition.MaxFields).When(x => x.Definition is not null).WithErrorCode("VAL.Fields.TooMany");
    }
}
