using JobPlatform.PlatformAdministration.Application.DTOs.Taxonomy;

namespace JobPlatform.PlatformAdministration.Application.Queries.Taxonomy;

public sealed record GetPlatformTaxonomyQuery(string Type, int? Version = null) : AdminQuery<TaxonomyView>;
