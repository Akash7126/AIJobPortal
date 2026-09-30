using JobPlatform.SharedKernel.Application.Interfaces.Cqrs;
using JobPlatform.SharedKernel.Application.Results;
using JobPlatform.SharedKernel.Security;

namespace JobPlatform.AccountIdentity.Application.Commands.Administration;

public sealed record ConfigurePasswordPolicyCommand(int MinLength, bool RequireUpper, bool RequireLower, bool RequireDigit, string? IfMatch = null)
    : AdminAuthorized(Permissions.PasswordPolicyManage), ICommand<Unit>;
