using JobPlatform.AuditLogging.Domain;

namespace JobPlatform.AuditLogging.Application.DTOs.Exports;

public sealed record ExportJobSpec(Guid JobId, ReportType ReportType, ExportFormat Format, IReadOnlyDictionary<string, string> Parameters);
