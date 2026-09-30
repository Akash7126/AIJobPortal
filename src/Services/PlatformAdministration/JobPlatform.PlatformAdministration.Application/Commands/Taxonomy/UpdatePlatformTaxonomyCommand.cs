using JobPlatform.PlatformAdministration.Application.DTOs.Common;
using JobPlatform.PlatformAdministration.Application.DTOs.Taxonomy;
using JobPlatform.PlatformAdministration.Application.Interfaces;

namespace JobPlatform.PlatformAdministration.Application.Commands.Taxonomy;

/// <summary>US-3.1.4-08: apply node changes as one saved version. The taxonomy of an unknown (but valid) type is created on first use (open set, GAP-003).</summary>
public sealed record UpdatePlatformTaxonomyCommand(string Type, IReadOnlyList<TaxonomyChangeRequest>? Changes) : AdminCommand<VersionResult>, ILaterSaveWinsCommand;
