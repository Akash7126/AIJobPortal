namespace JobPlatform.ExternalIntegration.Application.DTOs.Mapping;

public sealed record MappingRuleInput(string SourceField, string TargetField, string Transform);
