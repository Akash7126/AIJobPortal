using JobPlatform.AccountIdentity.Domain.Common;
using JobPlatform.SharedKernel.Application.Interfaces.Cqrs;
using JobPlatform.SharedKernel.Application.Results;
using JobPlatform.SharedKernel.Security;

namespace JobPlatform.AccountIdentity.Application.Commands.Accounts;

public sealed record ApprovePartnerAccountCommand(Guid AccountId)
    : AdminAuthorized(Permissions.AccountsApprovePartner, ErrorCodes.AdminForbidden), ICommand<Unit>;
