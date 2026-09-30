using JobPlatform.AccountIdentity.Application.Abstractions;
using JobPlatform.AccountIdentity.Application.DTOs.Accounts;
using JobPlatform.AccountIdentity.Application.DTOs.Administration;
using JobPlatform.AccountIdentity.Application.DTOs.ApiCredentials;
using JobPlatform.SharedKernel.ApiContracts.AccountIdentity;
using JobPlatform.SharedKernel.Application.Paging;

namespace JobPlatform.AccountIdentity.Application.Interfaces;

public interface IIdentityReadStore
{
    Task<AccountSummaryDto?> GetAccountSummaryAsync(Guid accountId, CancellationToken ct = default);

    Task<AccountStandingView?> GetAccountStandingAsync(Guid accountId, CancellationToken ct = default);

    Task<PagedResult<AccountListItemView>> ListAccountsAsync(AccountListFilter filter, PageRequest page, CancellationToken ct = default);

    Task<ApiCredentialControlsDto?> GetApiCredentialControlsAsync(Guid apiCredentialId, CancellationToken ct = default);

    Task<ApiCredentialView?> GetActiveApiCredentialForPartnerAsync(Guid partnerAccountId, CancellationToken ct = default);

    Task<PagedResult<AccessLogEntryDto>> ListAccessLogAsync(DateTime? fromUtc, DateTime? toUtc, Guid? accountId, PageRequest page, CancellationToken ct = default);

    Task<PasswordPolicyView> GetPasswordPolicyAsync(CancellationToken ct = default);

    Task<SessionTimeoutView> GetSessionTimeoutAsync(CancellationToken ct = default);

    Task<IReadOnlyList<RoleView>> ListRolesAsync(CancellationToken ct = default);

    Task<ConsentDecisionView?> GetConsentAsync(Guid guestId, string policyVersion, CancellationToken ct = default);
}
