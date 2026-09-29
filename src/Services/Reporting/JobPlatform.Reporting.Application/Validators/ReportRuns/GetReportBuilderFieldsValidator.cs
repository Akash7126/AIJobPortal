using FluentValidation;
using JobPlatform.Reporting.Application.Queries.ReportRuns;
using JobPlatform.Reporting.Application.Validators.Common;

namespace JobPlatform.Reporting.Application.Validators.ReportRuns;

public sealed class GetReportBuilderFieldsValidator : AbstractValidator<GetReportBuilderFieldsQuery>
{
    public GetReportBuilderFieldsValidator() =>
        RuleFor(x => x.DataSource).KnownDataSource();
}
