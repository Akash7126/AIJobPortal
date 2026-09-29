namespace JobPlatform.PlatformAdministration.Application.DTOs.Users;

public sealed record PlatformUserListItem(Guid AccountId, string ActorType, string DisplayName, string? Email, string? Mobile, string Standing, DateTime CreatedAtUtc);
