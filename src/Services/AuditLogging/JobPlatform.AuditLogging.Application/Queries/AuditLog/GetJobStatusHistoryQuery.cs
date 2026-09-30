using JobPlatform.AuditLogging.Application.DTOs.AuditLog;
using JobPlatform.AuditLogging.Domain;
using JobPlatform.SharedKernel.Application.Interfaces.Cqrs;
using JobPlatform.SharedKernel.Common.Enums;

namespace JobPlatform.AuditLogging.Application.Queries.AuditLog;

public sealed record GetJobStatusHistoryQuery(Guid JobPostingId) : ActorRequest(ActorType.Employer, AuditErrorCodes.JobStatusForbidden),
    IQuery<IReadOnlyList<JobStatusHistoryDto>>;
