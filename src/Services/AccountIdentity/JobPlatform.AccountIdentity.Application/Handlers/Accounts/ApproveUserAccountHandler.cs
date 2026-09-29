using JobPlatform.AccountIdentity.Application.Commands.Accounts;
using JobPlatform.AccountIdentity.Application.Services.Accounts;
using JobPlatform.SharedKernel.Application.Abstractions;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.AccountIdentity.Application.Handlers.Accounts;

internal sealed class ApproveUserAccountHandler : ICommandHandler<ApproveUserAccountCommand, Unit>
{
    private readonly TimeProvider _clock;
    private readonly AdminAccountService _adminAccountService;

    public ApproveUserAccountHandler(TimeProvider clock, AdminAccountService adminAccountService)
    {
        _clock = clock;
        _adminAccountService = adminAccountService;
    }

    public Task<Result<Unit>> Handle(ApproveUserAccountCommand request, CancellationToken ct) =>
        _adminAccountService.Run(request.AccountId, a => a.ApproveByAdministrator(_adminAccountService.Admin, _clock), invalidateSessions: false, request.IfMatch, ct);
}
