using JobPlatform.AccountIdentity.Domain.Common;
using JobPlatform.SharedKernel.Application.Abstractions;
using JobPlatform.SharedKernel.Application.Results;
using JobPlatform.SharedKernel.Security;

namespace JobPlatform.AccountIdentity.Application.Commands.Accounts;

public sealed record RemoveRoleFromAccountCommand(Guid AccountId, Guid RoleId, string? IfMatch = null)
    : AdminAuthorized(Permissions.AccountsAssignRoles, ErrorCodes.AdminForbidden), ICommand<Unit>;
