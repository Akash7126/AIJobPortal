using JobPlatform.SharedKernel.Application.Abstractions;

namespace JobPlatform.AuditLogging.Application.Commands.Exports;

public sealed record CompleteExportJobCommand(Guid JobId, string ResultRef) : ICommand;
