using JobPlatform.SharedKernel.Application.Interfaces.Cqrs;

namespace JobPlatform.AuditLogging.Application.Commands.Exports;

public sealed record CompleteExportJobCommand(Guid JobId, string ResultRef) : ICommand;
