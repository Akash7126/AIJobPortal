namespace JobPlatform.AuditLogging.Application.DTOs.AuditLog;

public sealed record EmployerDashboardDto(bool RegistrationApproved, int Postings, int ActivePostings, int Shortlists, IReadOnlyList<DashboardPostingDto> Items,
    DateTime? UpdatedAtUtc);
