using JobPlatform.PlatformAdministration.Application.DTOs.Reference;

namespace JobPlatform.PlatformAdministration.Application.Queries.Reference;

/// <summary>Consumers' read (GET /internal/v1/reference-files/{type}), Redis-cached for 30 minutes.</summary>
public sealed record GetReferenceFileForConsumersQuery(string Type) : ServiceQuery<ReferenceFileView>;
