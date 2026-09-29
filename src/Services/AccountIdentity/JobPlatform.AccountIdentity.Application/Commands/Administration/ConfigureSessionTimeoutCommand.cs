using JobPlatform.SharedKernel.Application.Abstractions;
using JobPlatform.SharedKernel.Application.Results;
using JobPlatform.SharedKernel.Security;

namespace JobPlatform.AccountIdentity.Application.Commands.Administration;

public sealed record ConfigureSessionTimeoutCommand(int IdleTimeoutMinutes, string? IfMatch = null) : AdminAuthorized(Permissions.SessionTimeoutManage), ICommand<Unit>;
