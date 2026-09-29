namespace JobPlatform.ExternalIntegration.Application.DTOs.Mapping;

public sealed record StandardSchemaView(string SchemaVersion, IReadOnlyList<StandardSchemaFieldView> Fields);
