namespace JobPlatform.PlatformAdministration.Application.DTOs.Offerings;

public sealed record JobOfferingListItem(
    Guid JobOfferingId, Guid EmployerId, string Title, string Status, string? Moderation, Guid? SuspendedBy, DateTime? SuspendedAtUtc, string? Reason,
    DateTime RegisteredAtUtc);
