using JobPlatform.AccountIdentity.Application.Commands.Accounts;
using JobPlatform.AccountIdentity.Application.Services.Accounts;
using JobPlatform.AccountIdentity.Domain.Common;
using JobPlatform.SharedKernel.Application.Abstractions;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.AccountIdentity.Application.Handlers.Accounts;

internal sealed class AssignRoleToAccountHandler : ICommandHandler<AssignRoleToAccountCommand, Unit>
{
    private readonly TimeProvider _clock;
    private readonly AdminAccountService _adminAccountService;

    public AssignRoleToAccountHandler(TimeProvider clock, AdminAccountService adminAccountService)
    {
        _clock = clock;
        _adminAccountService = adminAccountService;
    }

    public Task<Result<Unit>> Handle(AssignRoleToAccountCommand request, CancellationToken ct) =>
        _adminAccountService.Run(request.AccountId, a => a.AssignRole(new RoleId(request.RoleId), _clock), invalidateSessions: false, request.IfMatch, ct);
}
