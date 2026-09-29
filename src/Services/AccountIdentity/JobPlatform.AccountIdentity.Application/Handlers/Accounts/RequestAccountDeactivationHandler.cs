using JobPlatform.AccountIdentity.Application.Abstractions;
using JobPlatform.AccountIdentity.Application.Commands.Accounts;
using JobPlatform.AccountIdentity.Domain.Accounts;
using JobPlatform.AccountIdentity.Domain.Common;
using JobPlatform.SharedKernel.ApiContracts.AccountIdentity;
using JobPlatform.SharedKernel.Application.Abstractions;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.AccountIdentity.Application.Handlers.Accounts;

internal sealed class RequestAccountDeactivationHandler : ICommandHandler<RequestAccountDeactivationCommand, DeactivationRequestResultDto>
{
    private readonly IAccountRepository _accounts;
    private readonly ISessionStore _sessions;
    private readonly TimeProvider _clock;

    public RequestAccountDeactivationHandler(IAccountRepository accounts, ISessionStore sessions, TimeProvider clock)
    {
        _accounts = accounts;
        _sessions = sessions;
        _clock = clock;
    }

    public async Task<Result<DeactivationRequestResultDto>> Handle(RequestAccountDeactivationCommand request, CancellationToken ct)
    {
        var account = await _accounts.GetByIdAsync(new AccountId(request.AccountId), ct);
        if (account is null)
        {
            return AccountErrors.NotFound;
        }

        var kind = request.Kind == "Delete" ? SuspensionKind.DeletionRequested : SuspensionKind.Deactivated;
        account.RequestDeactivation(kind, request.Reason, _clock);
        await _sessions.InvalidateAllForAccountAsync(account.Id.Value, null, ct);
        return new DeactivationRequestResultDto(account.Id.Value, kind.ToString(), _clock.GetUtcNow().UtcDateTime);
    }
}
