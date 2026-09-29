using JobPlatform.SharedKernel.Application.Abstractions;

namespace JobPlatform.Reporting.Application.Commands.Activity;

/// <summary>Scheduled (leader-locked): removes activity facts past the retention period (AC-04). Not exposed over HTTP.</summary>
public sealed record RunRetentionJobCommand(int BatchSize) : ICommand<int>;
