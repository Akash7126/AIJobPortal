using FluentValidation;
using JobPlatform.Reporting.Application.Commands.ReportLibrary;
using JobPlatform.Reporting.Application.Validators.Common;
using JobPlatform.Reporting.Domain;

namespace JobPlatform.Reporting.Application.Validators.ReportLibrary;

/// <summary>SaveReportTemplateValidator (handover 7): unique parameter names, known types (min/max/default consistency is the domain's INV-03).</summary>
public sealed class SaveReportTemplateValidator : AbstractValidator<SaveReportTemplateCommand>
{
    public SaveReportTemplateValidator()
    {
        RuleFor(x => x.Name).ValidReportName();
        RuleFor(x => x.DataSource).KnownDataSource();
        RuleFor(x => x.Parameters).Must(p => p is null || p.Count <= 30).WithErrorCode("VAL.Parameters.TooMany");
        RuleFor(x => x.Parameters).Must(p => p is null || p.Select(x => x.Name?.ToLowerInvariant()).Distinct().Count() == p.Count).WithErrorCode("VAL.Parameters.DuplicateName");
        RuleForEach(x => x.Parameters).ChildRules(p =>
        {
            p.RuleFor(x => x.Name).NotEmpty().WithErrorCode("VAL.Parameter.Name.Required");
            p.RuleFor(x => x.Type).Must(t => Enum.TryParse<ParameterType>(t, true, out _)).WithErrorCode("VAL.Parameter.Type.Unknown");
        });
    }
}
