namespace JobPlatform.AuditLogging.Application.DTOs.AuditLog;

public sealed record AuditEntryDto(
    Guid Id, string Category, DateTime OccurredAtUtc, Guid? ActorId, string? ActorType, string SubjectType, string SubjectId,
    string Action, string Outcome, string? Code, IReadOnlyDictionary<string, string> Details, bool IsArchived);
