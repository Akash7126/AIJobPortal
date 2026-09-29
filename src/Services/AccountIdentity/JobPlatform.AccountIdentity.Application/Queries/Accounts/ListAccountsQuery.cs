using JobPlatform.AccountIdentity.Application.DTOs.Accounts;
using JobPlatform.AccountIdentity.Domain.Common;
using JobPlatform.SharedKernel.Application.Abstractions;
using JobPlatform.SharedKernel.Application.Paging;
using JobPlatform.SharedKernel.Common.Enums;
using JobPlatform.SharedKernel.Security;

namespace JobPlatform.AccountIdentity.Application.Queries.Accounts;

public sealed record ListAccountsQuery(ActorType? ActorType, string? Standing, string? Search, int Page = 1, int PageSize = 20)
    : AdminAuthorized(Permissions.AccountsRead, ErrorCodes.AdminForbidden), IQuery<PagedResult<AccountListItemView>>;
