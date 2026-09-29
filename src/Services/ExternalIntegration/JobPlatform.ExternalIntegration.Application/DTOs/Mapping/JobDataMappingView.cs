namespace JobPlatform.ExternalIntegration.Application.DTOs.Mapping;

public sealed record JobDataMappingView(
    Guid MappingId, Guid IntegrationId, int Version, string StandardSchemaVersion, IReadOnlyList<MappingRuleView> Rules);
