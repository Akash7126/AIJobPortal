using JobPlatform.SharedKernel.Common.Enums;

namespace JobPlatform.SharedKernel.ApiContracts.AccountIdentity;

public sealed record AccountSummaryDto(Guid AccountId, ActorType ActorType, string Standing, DateTime CreatedAtUtc, DateTime? ActivatedAtUtc);

public sealed record ApiCredentialControlsDto(
    Guid ApiCredentialId,
    Guid PartnerAccountId,
    string KeyId,
    string Status,
    IReadOnlyList<string> IpWhitelist,
    int MaxRequests,
    int PeriodSeconds,
    DateTime ExpiresAtUtc);

public sealed record AccessLogEntryDto(Guid Id, DateTime AtUtc, Guid? AccountId, string Action, string Resource, string Decision, string? Reason, string? IpAddress);

/// <param name="Kind">"Deactivate" or "Delete".</param>
public sealed record DeactivationRequestDto(string Kind, string Reason);

public sealed record DeactivationRequestResultDto(Guid AccountId, string Standing, DateTime RequestedAtUtc);

public sealed record PermissionCheckResultDto(Guid AccountId, string Permission, bool Allowed);
