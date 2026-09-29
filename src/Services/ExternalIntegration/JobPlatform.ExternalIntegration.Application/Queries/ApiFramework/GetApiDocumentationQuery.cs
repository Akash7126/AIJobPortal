using JobPlatform.ExternalIntegration.Application.DTOs.ApiFramework;

namespace JobPlatform.ExternalIntegration.Application.Queries.ApiFramework;

public sealed record GetApiDocumentationQuery(string Version) : PublicQuery<ApiDocumentationView>;
