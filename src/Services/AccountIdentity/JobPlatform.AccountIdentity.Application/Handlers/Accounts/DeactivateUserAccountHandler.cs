using JobPlatform.AccountIdentity.Application.Commands.Accounts;
using JobPlatform.AccountIdentity.Application.Services.Accounts;
using JobPlatform.SharedKernel.Application.Abstractions;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.AccountIdentity.Application.Handlers.Accounts;

internal sealed class DeactivateUserAccountHandler : ICommandHandler<DeactivateUserAccountCommand, Unit>
{
    private readonly TimeProvider _clock;
    private readonly AdminAccountService _adminAccountService;

    public DeactivateUserAccountHandler(TimeProvider clock, AdminAccountService adminAccountService)
    {
        _clock = clock;
        _adminAccountService = adminAccountService;
    }

    public Task<Result<Unit>> Handle(DeactivateUserAccountCommand request, CancellationToken ct) =>
        _adminAccountService.Run(request.AccountId, a => a.Deactivate(_adminAccountService.Admin, request.Reason, _clock), invalidateSessions: true, request.IfMatch, ct);
}
