using FluentValidation;
using JobPlatform.Reporting.Application.Commands.ReportLibrary;
using JobPlatform.Reporting.Application.Validators.Common;

namespace JobPlatform.Reporting.Application.Validators.ReportLibrary;

public sealed class SaveReportValidator : AbstractValidator<SaveReportCommand>
{
    public SaveReportValidator()
    {
        RuleFor(x => x.Name).ValidReportName();
        RuleFor(x => x.Definition).NotNull().WithErrorCode("VAL.Definition.Required");
    }
}
