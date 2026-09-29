using FluentValidation;
using JobPlatform.Reporting.Application.Commands.ReportRuns;
using JobPlatform.Reporting.Application.Validators.Common;

namespace JobPlatform.Reporting.Application.Validators.ReportRuns;

public sealed class RunCustomReportValidator : AbstractValidator<RunCustomReportCommand>
{
    public RunCustomReportValidator()
    {
        RuleFor(x => x).Must(x => (x.TemplateId is null) != (x.Definition is null)).OverridePropertyName("definition").WithErrorCode("VAL.Definition.ExactlyOne");
        RuleFor(x => x.Target).KnownTargetWhenPresent();
    }
}
