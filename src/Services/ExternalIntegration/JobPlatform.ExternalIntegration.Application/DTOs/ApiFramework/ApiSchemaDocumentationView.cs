namespace JobPlatform.ExternalIntegration.Application.DTOs.ApiFramework;

public sealed record ApiSchemaDocumentationView(string Version, string SchemaJson, bool Deprecated, DateTime? SunsetAtUtc);
