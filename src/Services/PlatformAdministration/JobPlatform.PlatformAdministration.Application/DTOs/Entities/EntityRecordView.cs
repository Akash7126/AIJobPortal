namespace JobPlatform.PlatformAdministration.Application.DTOs.Entities;

public sealed record EntityRecordView(Guid Id, string EntityType, string IdentityKey, string Status, Guid CreatedBy, DateTime CreatedAtUtc);
