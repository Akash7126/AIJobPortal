using JobPlatform.AccountIdentity.Application.Commands.Accounts;
using JobPlatform.AccountIdentity.Application.Services.Accounts;
using JobPlatform.SharedKernel.Application.Abstractions;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.AccountIdentity.Application.Handlers.Accounts;

internal sealed class BanUserAccountHandler : ICommandHandler<BanUserAccountCommand, Unit>
{
    private readonly TimeProvider _clock;
    private readonly AdminAccountService _adminAccountService;

    public BanUserAccountHandler(TimeProvider clock, AdminAccountService adminAccountService)
    {
        _clock = clock;
        _adminAccountService = adminAccountService;
    }

    public Task<Result<Unit>> Handle(BanUserAccountCommand request, CancellationToken ct) =>
        _adminAccountService.Run(request.AccountId, a => a.Ban(_adminAccountService.Admin, request.Reason, _clock), invalidateSessions: true, request.IfMatch, ct);
}
