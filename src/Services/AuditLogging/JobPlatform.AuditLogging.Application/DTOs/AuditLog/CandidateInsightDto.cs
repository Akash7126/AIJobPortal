namespace JobPlatform.AuditLogging.Application.DTOs.AuditLog;

public sealed record CandidateInsightDto(Guid CandidateId, Guid JobPostingId, string Availability, string ExpectedSalary, string Fit, DateTime ComputedAtUtc);
