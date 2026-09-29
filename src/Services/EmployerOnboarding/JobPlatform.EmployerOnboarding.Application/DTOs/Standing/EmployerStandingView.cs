namespace JobPlatform.EmployerOnboarding.Application.DTOs.Standing;

public sealed record EmployerStandingView(Guid EmployerAccountId, bool Approved, bool Verified, string? Badge);
