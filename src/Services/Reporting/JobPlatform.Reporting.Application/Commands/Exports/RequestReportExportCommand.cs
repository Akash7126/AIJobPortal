using JobPlatform.Reporting.Application.DTOs.Exports;
using JobPlatform.Reporting.Domain;
using JobPlatform.SharedKernel.Application.Interfaces.Cqrs;

namespace JobPlatform.Reporting.Application.Commands.Exports;

// US-3.5.4-05 export (also BC-07's Administrator Report, Q-05).

/// <summary>Idempotent (INV-06): an identical request while one is Queued or Generating returns that job (Reused = true) instead of starting another.</summary>
public sealed record RequestReportExportCommand(string RefKind, Guid? RefId, IReadOnlyDictionary<string, string>? Parameters, string Format)
    : CustomCommandRequest, ICommand<ExportDto>, IConflictAwareCommand
{
    public string UniqueViolationErrorCode => ReportingErrorCodes.ExportDuplicate;
}
