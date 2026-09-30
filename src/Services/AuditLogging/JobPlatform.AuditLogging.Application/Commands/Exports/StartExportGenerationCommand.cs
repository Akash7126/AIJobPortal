using JobPlatform.AuditLogging.Application.DTOs.Exports;
using JobPlatform.SharedKernel.Application.Interfaces.Cqrs;

namespace JobPlatform.AuditLogging.Application.Commands.Exports;

public sealed record StartExportGenerationCommand(Guid JobId) : ICommand<ExportJobSpec>;
