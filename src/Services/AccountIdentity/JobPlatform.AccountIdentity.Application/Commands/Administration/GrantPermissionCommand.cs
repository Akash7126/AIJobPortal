using JobPlatform.SharedKernel.Application.Interfaces.Cqrs;
using JobPlatform.SharedKernel.Application.Results;
using JobPlatform.SharedKernel.Security;

namespace JobPlatform.AccountIdentity.Application.Commands.Administration;

public sealed record GrantPermissionCommand(Guid RoleId, string Permission, string? IfMatch = null) : AdminAuthorized(Permissions.RolesManage), ICommand<Unit>;
