using JobPlatform.AccountIdentity.Application.Commands.Accounts;
using JobPlatform.AccountIdentity.Application.Services.Accounts;
using JobPlatform.SharedKernel.Application.Interfaces.Cqrs;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.AccountIdentity.Application.Handlers.Accounts;

internal sealed class ResetCredentialsHandler : ICommandHandler<ResetCredentialsCommand, Unit>
{
    private readonly TimeProvider _clock;
    private readonly AdminAccountService _adminAccountService;

    public ResetCredentialsHandler(TimeProvider clock, AdminAccountService adminAccountService)
    {
        _clock = clock;
        _adminAccountService = adminAccountService;
    }

    public Task<Result<Unit>> Handle(ResetCredentialsCommand request, CancellationToken ct) =>
        _adminAccountService.Run(request.AccountId, a => a.ResetCredentials(_adminAccountService.Admin, _clock), invalidateSessions: true, request.IfMatch, ct);
}
