using JobPlatform.ExternalIntegration.Application.DTOs.Integrations;

namespace JobPlatform.ExternalIntegration.Application.Commands.Integrations;

public sealed record RegisterExternalJobSiteCommand(string SourcePlatformName, string BaseUrl, bool RecommendedByMolPef)
    : PartnerCommand<IntegrationView>;
