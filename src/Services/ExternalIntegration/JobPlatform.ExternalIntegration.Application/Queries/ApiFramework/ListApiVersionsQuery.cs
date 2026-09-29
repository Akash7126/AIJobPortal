using JobPlatform.ExternalIntegration.Application.DTOs.ApiFramework;

namespace JobPlatform.ExternalIntegration.Application.Queries.ApiFramework;

public sealed record ListApiVersionsQuery : PublicQuery<IReadOnlyList<ApiVersionView>>;
