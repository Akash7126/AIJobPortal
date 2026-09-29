using JobPlatform.PlatformAdministration.Application.DTOs.Entities;
using JobPlatform.PlatformAdministration.Domain.Common;
using JobPlatform.PlatformAdministration.Domain.Entities;
using JobPlatform.SharedKernel.Application.Abstractions;

namespace JobPlatform.PlatformAdministration.Application.Commands.Entities;

/// <summary>US-3.1.4-02: an administrator creates a job seeker, employer or job offering record. Accepts Idempotency-Key.</summary>
public sealed record CreatePlatformEntityRecordCommand(PlatformEntityType EntityType, Dictionary<string, string>? Core, string? IdempotencyKey = null)
    : AdminCommand<EntityRecordView>, IIdempotentCommand, IConflictAwareCommand
{
    public string UniqueViolationErrorCode => ErrorCodes.Duplicate;
}
