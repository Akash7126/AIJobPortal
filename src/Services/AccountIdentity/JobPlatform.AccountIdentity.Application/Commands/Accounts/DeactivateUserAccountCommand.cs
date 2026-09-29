using JobPlatform.AccountIdentity.Domain.Common;
using JobPlatform.SharedKernel.Application.Abstractions;
using JobPlatform.SharedKernel.Application.Results;
using JobPlatform.SharedKernel.Security;

namespace JobPlatform.AccountIdentity.Application.Commands.Accounts;

public sealed record DeactivateUserAccountCommand(Guid AccountId, string Reason, string? IfMatch = null)
    : AdminAuthorized(Permissions.AccountsDeactivate, ErrorCodes.AdminForbidden), ICommand<Unit>;
