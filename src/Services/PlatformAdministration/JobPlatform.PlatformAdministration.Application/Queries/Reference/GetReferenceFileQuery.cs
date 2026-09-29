using JobPlatform.PlatformAdministration.Application.DTOs.Reference;

namespace JobPlatform.PlatformAdministration.Application.Queries.Reference;

public sealed record GetReferenceFileQuery(string Type) : AdminQuery<ReferenceFileView>;
