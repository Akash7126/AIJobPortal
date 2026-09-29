namespace JobPlatform.AuditLogging.Application.DTOs.Exports;

public sealed record ExportJobDto(Guid Id, string ReportType, string Format, string Status, string? ResultRef, string? FailureReason, DateTime RequestedAtUtc,
    DateTime? CompletedAtUtc);
