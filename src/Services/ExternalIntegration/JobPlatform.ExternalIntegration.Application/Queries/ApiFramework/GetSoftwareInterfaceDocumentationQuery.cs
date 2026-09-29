using JobPlatform.ExternalIntegration.Application.DTOs.ApiFramework;

namespace JobPlatform.ExternalIntegration.Application.Queries.ApiFramework;

public sealed record GetSoftwareInterfaceDocumentationQuery(string Version) : PartnerQuery<IReadOnlyList<SoftwareInterfaceView>>;
