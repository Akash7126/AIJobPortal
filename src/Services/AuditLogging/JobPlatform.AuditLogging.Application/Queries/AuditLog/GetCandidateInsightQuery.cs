using JobPlatform.AuditLogging.Application.DTOs.AuditLog;
using JobPlatform.AuditLogging.Domain;
using JobPlatform.SharedKernel.Application.Abstractions;
using JobPlatform.SharedKernel.Common.Enums;

namespace JobPlatform.AuditLogging.Application.Queries.AuditLog;

public sealed record GetCandidateInsightQuery(Guid CandidateId, Guid JobPostingId) : ActorRequest(ActorType.Employer, AuditErrorCodes.InsightForbidden),
    IQuery<CandidateInsightDto>;
