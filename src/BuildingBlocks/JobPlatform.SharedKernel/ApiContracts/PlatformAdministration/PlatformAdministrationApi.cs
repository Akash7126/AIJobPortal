namespace JobPlatform.SharedKernel.ApiContracts.PlatformAdministration;

public sealed record TaxonomyDto(string Type, string Version, IReadOnlyList<TaxonomyEntryDto> Entries);

public sealed record TaxonomyEntryDto(string Code, string LabelEn, string LabelAr, IReadOnlyList<string> Synonyms, bool IsActive, string? ParentCode = null);
