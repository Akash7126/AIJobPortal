namespace JobPlatform.PlatformAdministration.Application.DTOs.Reference;

public sealed record ReferenceFileView(Guid ReferenceFileId, string Type, int Version, IReadOnlyList<ReferenceEntryView> Entries);
