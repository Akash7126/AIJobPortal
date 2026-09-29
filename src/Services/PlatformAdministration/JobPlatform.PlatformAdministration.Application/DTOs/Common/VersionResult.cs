namespace JobPlatform.PlatformAdministration.Application.DTOs.Common;

/// <summary>Result of a command that bumps a version (settings, reference files, taxonomies).</summary>
public sealed record VersionResult(int Version);
