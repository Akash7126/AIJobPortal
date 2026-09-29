namespace JobPlatform.Reporting.Application.DTOs.Exports;

public sealed record ExportDto(Guid Id, string RefKind, Guid? RefId, string Format, string Status, string? ResultRef, string? FailureReason, DateTime RequestedAtUtc,
    DateTime? CompletedAtUtc, bool Reused);
