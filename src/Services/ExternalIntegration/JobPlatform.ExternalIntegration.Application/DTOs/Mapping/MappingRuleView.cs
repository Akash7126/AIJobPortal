namespace JobPlatform.ExternalIntegration.Application.DTOs.Mapping;

public sealed record MappingRuleView(string SourceField, string TargetField, string Transform);
