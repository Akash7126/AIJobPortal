using JobPlatform.GovernmentIntegration.Application.DTOs.Verifications;
using JobPlatform.GovernmentIntegration.Domain;

namespace JobPlatform.GovernmentIntegration.Application.Queries.Verifications;

public sealed record GetSubjectVerificationStatusQuery(SubjectType SubjectType, Guid SubjectId) : ServiceQuery<SubjectVerificationStatusView>;
