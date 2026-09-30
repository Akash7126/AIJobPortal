using JobPlatform.AuditLogging.Application.DTOs.Exports;
using JobPlatform.AuditLogging.Domain;
using JobPlatform.SharedKernel.Application.Interfaces.Cqrs;

namespace JobPlatform.AuditLogging.Application.Commands.Exports;

/// <param name="ReportType">One of the ReportType names (PostingsBySector, ...).</param>
/// <param name="Format">Csv, Json or Xml (THR-081).</param>
public sealed record RequestAdministratorReportExportCommand(string ReportType, string Format, IReadOnlyDictionary<string, string>? Parameters)
    : AdminRequest(AuditErrorCodes.AdminForbidden), ICommand<ExportRequestResult>, IConflictAwareCommand
{
    public string UniqueViolationErrorCode => "E-AUDIT-EXPORT-DUPLICATE";
}
