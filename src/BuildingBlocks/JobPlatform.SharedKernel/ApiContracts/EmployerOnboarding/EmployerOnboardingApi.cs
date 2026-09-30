namespace JobPlatform.SharedKernel.ApiContracts.EmployerOnboarding;

public sealed record EmployerStandingDto(Guid EmployerAccountId, bool Approved, bool Verified, string? Badge);
