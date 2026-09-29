namespace JobPlatform.EmployerOnboarding.Application.DTOs.Standing;

public sealed record CompanyPublicInfoView(Guid EmployerAccountId, string Name, string? LogoUrl, string Industry, string Size, string Website);
