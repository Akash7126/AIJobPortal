using JobPlatform.PlatformAdministration.Application.DTOs.Common;

namespace JobPlatform.PlatformAdministration.Application.DTOs.Reference;

public sealed record ReferenceEntryView(Guid EntryId, string Code, LocalizedNameView Name, bool IsActive);
