using JobPlatform.PlatformAdministration.Application.DTOs.Common;

namespace JobPlatform.PlatformAdministration.Application.DTOs.Taxonomy;

public sealed record TaxonomyNodeView(string Code, LocalizedNameView Name, string? ParentCode, IReadOnlyList<string> Synonyms, bool IsActive);
