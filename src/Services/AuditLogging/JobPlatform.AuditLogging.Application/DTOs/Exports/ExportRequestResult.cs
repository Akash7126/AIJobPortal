namespace JobPlatform.AuditLogging.Application.DTOs.Exports;

/// <summary>Reused = an identical request was already queued or generating and that job is returned instead of starting another (AC-03).</summary>
public sealed record ExportRequestResult(ExportJobDto Job, bool Reused);
