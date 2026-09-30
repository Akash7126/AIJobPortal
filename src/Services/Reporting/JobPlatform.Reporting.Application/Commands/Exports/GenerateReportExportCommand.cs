using JobPlatform.Reporting.Application.DTOs.Exports;
using JobPlatform.SharedKernel.Application.Interfaces.Cqrs;

namespace JobPlatform.Reporting.Application.Commands.Exports;

/// <summary>Worker command: generates the file of one queued export and drives it to Ready or Failed.</summary>
public sealed record GenerateReportExportCommand(Guid ExportId) : ICommand<ExportDto>;
