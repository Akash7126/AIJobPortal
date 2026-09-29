using JobPlatform.AccountIdentity.Domain.Common;
using JobPlatform.SharedKernel.Application.Abstractions;
using JobPlatform.SharedKernel.Application.Results;
using JobPlatform.SharedKernel.Security;

namespace JobPlatform.AccountIdentity.Application.Commands.Accounts;

public sealed record ApproveUserAccountCommand(Guid AccountId, string? IfMatch = null) : AdminAuthorized(Permissions.AccountsApprove, ErrorCodes.AdminForbidden), ICommand<Unit>;
