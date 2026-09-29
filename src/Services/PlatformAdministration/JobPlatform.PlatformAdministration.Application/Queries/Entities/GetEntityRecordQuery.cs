using JobPlatform.PlatformAdministration.Application.DTOs.Entities;

namespace JobPlatform.PlatformAdministration.Application.Queries.Entities;

public sealed record GetEntityRecordQuery(Guid Id) : AdminQuery<EntityRecordView>;
