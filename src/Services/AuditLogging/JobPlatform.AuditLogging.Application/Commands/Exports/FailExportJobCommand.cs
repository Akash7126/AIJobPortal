using JobPlatform.SharedKernel.Application.Interfaces.Cqrs;

namespace JobPlatform.AuditLogging.Application.Commands.Exports;

public sealed record FailExportJobCommand(Guid JobId, string Reason) : ICommand;
