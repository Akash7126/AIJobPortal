using JobPlatform.ExternalIntegration.Application.DTOs.Integrations;

namespace JobPlatform.ExternalIntegration.Application.Queries.Integrations;

/// <summary>US-3.1.3-06/10/11 collaboration surface: BC-07/BC-09 read the integration summary rather than a live call (handover 6.1).</summary>
public sealed record GetIntegrationSummaryQuery(Guid SourcePlatformId) : ServiceQuery<IntegrationSummaryView>;
