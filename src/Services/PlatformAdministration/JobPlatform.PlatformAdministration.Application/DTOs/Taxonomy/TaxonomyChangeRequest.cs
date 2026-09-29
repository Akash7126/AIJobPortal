using JobPlatform.PlatformAdministration.Application.DTOs.Common;

namespace JobPlatform.PlatformAdministration.Application.DTOs.Taxonomy;

/// <param name="Op">add, edit or remove (soft-remove).</param>
public sealed record TaxonomyChangeRequest(string Op, string Code, LocalizedNameView? Name, string? ParentCode, IReadOnlyList<string>? Synonyms, bool? IsActive);
