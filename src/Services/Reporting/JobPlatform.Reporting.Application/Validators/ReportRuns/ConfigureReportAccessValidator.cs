using FluentValidation;
using JobPlatform.Reporting.Application.Commands.ReportRuns;
using JobPlatform.Reporting.Domain;

namespace JobPlatform.Reporting.Application.Validators.ReportRuns;

public sealed class ConfigureReportAccessValidator : AbstractValidator<ConfigureReportAccessCommand>
{
    public ConfigureReportAccessValidator()
    {
        RuleFor(x => x.Role).NotEmpty().WithErrorCode("VAL.Role.Required")
            .Must(r => r is null || r.Equals(ReportAccessRule.AdministratorRole, StringComparison.OrdinalIgnoreCase) || Guid.TryParse(r, out _)).WithErrorCode("VAL.Role.Unknown");
        RuleFor(x => x.Categories).NotNull().WithErrorCode("VAL.Categories.Required");
        RuleForEach(x => x.Categories).Must(c => Enum.TryParse<ReportCategory>(c, true, out _)).WithErrorCode("VAL.Category.Unknown");
    }
}
