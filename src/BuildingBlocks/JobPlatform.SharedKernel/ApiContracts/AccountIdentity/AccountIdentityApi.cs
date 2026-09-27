using JobPlatform.SharedKernel.Application.Paging;
using JobPlatform.SharedKernel.Common.Enums;

namespace JobPlatform.SharedKernel.ApiContracts.AccountIdentity;

/// <summary>Synchronous contract of BC-03 for other BCs (routes under /internal/v1). Consumers implement it with a typed HttpClient.</summary>
public interface IAccountIdentityApi
{
    Task<AccountSummaryDto?> GetAccountAsync(Guid accountId, CancellationToken ct = default);

    Task<ApiCredentialControlsDto?> GetApiCredentialControlsAsync(Guid apiCredentialId, CancellationToken ct = default);

    Task<PagedResult<AccessLogEntryDto>> ListAccessLogAsync(DateTime? fromUtc, DateTime? toUtc, Guid? accountId, PageRequest page, CancellationToken ct = default);

    Task<DeactivationRequestResultDto> RequestDeactivationAsync(Guid accountId, DeactivationRequestDto request, CancellationToken ct = default);

    Task<PermissionCheckResultDto> CheckPermissionAsync(Guid accountId, string permission, CancellationToken ct = default);
}

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
