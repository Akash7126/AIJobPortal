using JobPlatform.SharedKernel.Application.Abstractions;

namespace JobPlatform.AuditLogging.Application.Commands.Exports;

/// <summary>Archives entries whose retain-until instant has passed (kept, not purged). Scheduled by the infrastructure; a system command with no caller.</summary>
public sealed record ArchiveExpiredAuditEntriesCommand(int BatchSize) : ICommand<int>;
