namespace JobPlatform.GovernmentIntegration.Application.DTOs.EmployerVerifications;

public sealed record VerificationAttemptView(int AttemptNo, string Source, string Outcome, string? ErrorCode, DateTime StartedAtUtc);
