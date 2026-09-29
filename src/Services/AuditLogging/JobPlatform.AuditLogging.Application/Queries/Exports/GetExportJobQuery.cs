using JobPlatform.AuditLogging.Application.DTOs.Exports;
using JobPlatform.AuditLogging.Domain;
using JobPlatform.SharedKernel.Application.Abstractions;

namespace JobPlatform.AuditLogging.Application.Queries.Exports;

public sealed record GetExportJobQuery(Guid Id) : AdminRequest(AuditErrorCodes.AdminForbidden), IQuery<ExportJobDto>;
