using FluentValidation;
using JobPlatform.AuditLogging.Application.Commands.Exports;
using JobPlatform.AuditLogging.Domain;

namespace JobPlatform.AuditLogging.Application.Validators.Exports;

public sealed class RequestAdministratorReportExportValidator : AbstractValidator<RequestAdministratorReportExportCommand>
{
    public RequestAdministratorReportExportValidator()
    {
        RuleFor(x => x.ReportType).NotEmpty().WithErrorCode("VAL.ReportType.Required").DependentRules(() =>
            RuleFor(x => x.ReportType).Must(t => Enum.TryParse<ReportType>(t, true, out _)).WithErrorCode("VAL.ReportType.Invalid"));
        RuleFor(x => x.Format).NotEmpty().WithErrorCode("VAL.Format.Required").DependentRules(() =>
            RuleFor(x => x.Format).Must(f => Enum.TryParse<ExportFormat>(f, true, out _)).WithErrorCode("VAL.Format.Invalid"));
        When(x => x.Parameters is not null, () =>
            RuleFor(x => x.Parameters!).Must(p => p.Count <= 20 && p.All(kv => kv.Key.Length <= 50 && kv.Value.Length <= 200)).WithErrorCode("VAL.Parameters.Invalid"));
    }
}
