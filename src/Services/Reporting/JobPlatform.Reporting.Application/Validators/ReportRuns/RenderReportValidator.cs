using FluentValidation;
using JobPlatform.Reporting.Application.Queries.ReportRuns;

namespace JobPlatform.Reporting.Application.Validators.ReportRuns;

public sealed class RenderReportValidator : AbstractValidator<RenderReportQuery>
{
    public RenderReportValidator()
    {
        RuleFor(x => x.Format).Must(ReportViewBuilder.IsKnown).WithErrorCode("VAL.Format.Unknown");
        RuleFor(x => x).Must(x => (x.TemplateId is null) != (x.Definition is null)).OverridePropertyName("definition").WithErrorCode("VAL.Definition.ExactlyOne");
    }
}
