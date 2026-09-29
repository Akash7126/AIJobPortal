using JobPlatform.AccountIdentity.Application.DTOs.Accounts;
using JobPlatform.AccountIdentity.Domain.Common;
using JobPlatform.SharedKernel.Application.Abstractions;
using JobPlatform.SharedKernel.Security;

namespace JobPlatform.AccountIdentity.Application.Queries.Accounts;

public sealed record GetAccountStandingQuery(Guid AccountId) : AdminAuthorized(Permissions.AccountsRead, ErrorCodes.AdminForbidden), IQuery<AccountStandingView>;
