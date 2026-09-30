using JobPlatform.SharedKernel.ApiContracts.AccountIdentity;
using JobPlatform.SharedKernel.Application.Interfaces.Cqrs;

namespace JobPlatform.AccountIdentity.Application.Queries.Accounts;

public sealed record GetAccountSummaryQuery(Guid AccountId) : ServiceAuthorized, IQuery<AccountSummaryDto>;
