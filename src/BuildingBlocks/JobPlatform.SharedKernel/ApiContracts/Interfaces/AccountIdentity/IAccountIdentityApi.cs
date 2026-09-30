using JobPlatform.SharedKernel.ApiContracts.AccountIdentity;
using JobPlatform.SharedKernel.Application.Paging;

namespace JobPlatform.SharedKernel.ApiContracts.Interfaces.AccountIdentity;

/// <summary>Synchronous contract of BC-03 for other BCs (routes under /internal/v1). Consumers implement it with a typed HttpClient.</summary>
public interface IAccountIdentityApi
{
    Task<AccountSummaryDto?> GetAccountAsync(Guid accountId, CancellationToken ct = default);

    Task<ApiCredentialControlsDto?> GetApiCredentialControlsAsync(Guid apiCredentialId, CancellationToken ct = default);

    Task<PagedResult<AccessLogEntryDto>> ListAccessLogAsync(DateTime? fromUtc, DateTime? toUtc, Guid? accountId, PageRequest page, CancellationToken ct = default);

    Task<DeactivationRequestResultDto> RequestDeactivationAsync(Guid accountId, DeactivationRequestDto request, CancellationToken ct = default);

    Task<PermissionCheckResultDto> CheckPermissionAsync(Guid accountId, string permission, CancellationToken ct = default);
}
