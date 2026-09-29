namespace JobPlatform.EmployerOnboarding.Application.DTOs.Registration;

public sealed record EmployerRegistrationView(
    Guid EmployerRegistrationId, Guid EmployerAccountId, string Status, CompanyIdentityView? Identity, Level2View? Level2, DateTime OpenedAtUtc,
    Guid? ApprovedBy, DateTime? ApprovedAtUtc, byte[] RowVersion);
