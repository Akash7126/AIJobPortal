using JobPlatform.PlatformAdministration.Application.DTOs.Common;

namespace JobPlatform.PlatformAdministration.Application.DTOs.Reference;

/// <param name="Op">add, edit or remove.</param>
public sealed record ReferenceChangeRequest(string Op, Guid? EntryId, string? Code, LocalizedNameView? Name, bool? IsActive);
