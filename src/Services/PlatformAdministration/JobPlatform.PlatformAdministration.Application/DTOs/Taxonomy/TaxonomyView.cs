namespace JobPlatform.PlatformAdministration.Application.DTOs.Taxonomy;

public sealed record TaxonomyView(Guid TaxonomyId, string Type, int Version, IReadOnlyList<TaxonomyNodeView> Nodes);
