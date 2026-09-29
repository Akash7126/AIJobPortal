using FluentValidation;
using JobPlatform.Reporting.Application.Commands.ReportRuns;
using JobPlatform.Reporting.Application.Validators.Common;

namespace JobPlatform.Reporting.Application.Validators.ReportRuns;

public sealed class RunSavedReportValidator : AbstractValidator<RunSavedReportCommand>
{
    public RunSavedReportValidator() =>
        RuleFor(x => x.Target).KnownTargetWhenPresent();
}
