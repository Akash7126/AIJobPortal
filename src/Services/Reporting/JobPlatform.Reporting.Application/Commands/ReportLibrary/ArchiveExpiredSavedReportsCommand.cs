using JobPlatform.SharedKernel.Application.Interfaces.Cqrs;

namespace JobPlatform.Reporting.Application.Commands.ReportLibrary;

/// <summary>Scheduled: archives saved reports past their 12-month retention (AC-03). Returns the number archived.</summary>
public sealed record ArchiveExpiredSavedReportsCommand(int BatchSize) : ICommand<int>;
