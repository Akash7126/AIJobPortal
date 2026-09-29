using JobPlatform.SharedKernel.Application.Abstractions;

namespace JobPlatform.AuditLogging.Application.Commands.Exports;

public sealed record FailExportJobCommand(Guid JobId, string Reason) : ICommand;
