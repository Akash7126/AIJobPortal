using JobPlatform.PlatformAdministration.Application.DTOs.Taxonomy;

namespace JobPlatform.PlatformAdministration.Application.Queries.Taxonomy;

/// <summary>Consumers' read (GET /internal/v1/taxonomies/{type}?version=): the current version pointer (5 min) and immutable per-version content (60 min) are cached.</summary>
public sealed record GetTaxonomyForConsumersQuery(string Type, int? Version = null) : ServiceQuery<TaxonomyView>;
