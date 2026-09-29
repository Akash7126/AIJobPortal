using FluentValidation;
using JobPlatform.Reporting.Application.Commands.Exports;
using JobPlatform.Reporting.Application.Validators.Common;
using JobPlatform.Reporting.Domain;

namespace JobPlatform.Reporting.Application.Validators.Exports;

/// <summary>RequestReportExportValidator (handover 7): format and reference kind known; the referenced report is checked to exist by the handler.</summary>
public sealed class RequestReportExportValidator : AbstractValidator<RequestReportExportCommand>
{
    public RequestReportExportValidator()
    {
        RuleFor(x => x.Format).KnownReportFormat();
        RuleFor(x => x.RefKind).Must(k => Enum.TryParse<ReportRefKind>(k, true, out _)).WithErrorCode("VAL.RefKind.Unknown");
        RuleFor(x => x.RefId).NotNull().When(x => Enum.TryParse<ReportRefKind>(x.RefKind, true, out var k) && k != ReportRefKind.LaborMarket).WithErrorCode("VAL.RefId.Required");
        RuleFor(x => x.Parameters).Must(p => p is null || (p.Count <= 20 && p.All(kv => kv.Key.Length <= 50 && kv.Value.Length <= 200))).WithErrorCode("VAL.Parameters.Invalid");
    }
}
