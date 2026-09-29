namespace JobPlatform.GovernmentIntegration.Application.DTOs.EmployerVerifications;

public sealed record EmployerVerificationView(
    Guid EmployerVerificationId, Guid EmployerAccountId, string State, string Method, int AttemptCount, Guid? DecidedBy, DateTime? DecidedAtUtc,
    string? FailureReason, IReadOnlyList<VerificationAttemptView> Attempts, byte[] RowVersion);
