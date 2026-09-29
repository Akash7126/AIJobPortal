using JobPlatform.HelpContent.Application.DTOs.Common;

namespace JobPlatform.HelpContent.Application.DTOs.CompanyPage;

public sealed record CompanyPageView(
    Guid EmployerAccountId, string Name, string? LogoUrl, string Industry, string CompanySize, string Website, bool Verified, string? Badge,
    LocalizedView Background, IReadOnlyList<string> Highlights, IReadOnlyList<OpenPostingView> OpenPostings, bool OpenPostingsDegraded);
