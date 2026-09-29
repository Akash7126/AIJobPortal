namespace JobPlatform.GovernmentIntegration.Application.DTOs.Verifications;

public sealed record SubjectVerificationStatusView(string SubjectType, Guid SubjectId, string? GovernmentDataStatus, string? EducationalStatus,
    string? IdentityStatus);
