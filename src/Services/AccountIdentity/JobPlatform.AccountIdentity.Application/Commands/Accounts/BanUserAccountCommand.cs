using JobPlatform.AccountIdentity.Domain.Common;
using JobPlatform.SharedKernel.Application.Interfaces.Cqrs;
using JobPlatform.SharedKernel.Application.Results;
using JobPlatform.SharedKernel.Security;

namespace JobPlatform.AccountIdentity.Application.Commands.Accounts;

public sealed record BanUserAccountCommand(Guid AccountId, string Reason, string? IfMatch = null) : AdminAuthorized(Permissions.AccountsBan, ErrorCodes.AdminForbidden), ICommand<Unit>;
