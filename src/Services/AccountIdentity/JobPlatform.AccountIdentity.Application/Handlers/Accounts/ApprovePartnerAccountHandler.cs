using JobPlatform.AccountIdentity.Application.Commands.Accounts;
using JobPlatform.AccountIdentity.Domain.Common;
using JobPlatform.SharedKernel.Application.Abstractions;
using JobPlatform.SharedKernel.Application.Ports;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.AccountIdentity.Application.Handlers.Accounts;

internal sealed class ApprovePartnerAccountHandler : ICommandHandler<ApprovePartnerAccountCommand, Unit>
{
    private readonly IAccountRepository _accounts;
    private readonly ICurrentUser _user;
    private readonly TimeProvider _clock;

    public ApprovePartnerAccountHandler(IAccountRepository accounts, ICurrentUser user, TimeProvider clock)
    {
        _accounts = accounts;
        _user = user;
        _clock = clock;
    }

    public async Task<Result<Unit>> Handle(ApprovePartnerAccountCommand request, CancellationToken ct)
    {
        var account = await _accounts.GetByIdAsync(new AccountId(request.AccountId), ct);
        if (account is null)
        {
            return AccountErrors.NotFound;
        }

        // The pipeline has already verified the approve-partner permission (Q-07), which is what "authorised staff" means.
        account.ApproveByStaff(Actor.AuthorisedStaff(_user.UserId!.Value), _clock);
        return Result.Success();
    }
}
