using JobPlatform.AccountIdentity.Application.Commands.Authentication;
using JobPlatform.AccountIdentity.Application.Interfaces;
using JobPlatform.AccountIdentity.Domain.Accounts;
using JobPlatform.AccountIdentity.Domain.Common;
using JobPlatform.AccountIdentity.Domain.Interfaces.Repositories;
using JobPlatform.SharedKernel.Application.Interfaces.Cqrs;
using JobPlatform.SharedKernel.Application.Interfaces.Ports;
using JobPlatform.SharedKernel.Application.Results;
using JobPlatform.SharedKernel.Domain;

namespace JobPlatform.AccountIdentity.Application.Handlers.Authentication;

internal sealed class ChangePasswordHandler : ICommandHandler<ChangePasswordCommand, Unit>
{
    private readonly IAccountRepository _accounts;
    private readonly IPasswordPolicyRepository _policies;
    private readonly IPasswordHasher _hasher;
    private readonly ISessionStore _sessions;
    private readonly ICurrentUser _user;
    private readonly TimeProvider _clock;

    public ChangePasswordHandler(IAccountRepository accounts, IPasswordPolicyRepository policies, IPasswordHasher hasher, ISessionStore sessions,
        ICurrentUser user, TimeProvider clock)
    {
        _accounts = accounts;
        _policies = policies;
        _hasher = hasher;
        _sessions = sessions;
        _user = user;
        _clock = clock;
    }

    public async Task<Result<Unit>> Handle(ChangePasswordCommand request, CancellationToken ct)
    {
        var account = await _accounts.GetByIdAsync(new AccountId(_user.UserId!.Value), ct);
        if (account is null)
        {
            return AccountErrors.NotFound;
        }

        var currentMatches = _hasher.Verify(request.CurrentPassword, account.PasswordHash.Value);
        var attempt = account.AttemptLogin(currentMatches, _clock);
        if (!attempt.IsSuccess)
        {
            return LoginErrors.From(attempt, _clock.GetUtcNow().UtcDateTime);
        }

        var policy = await _policies.GetAsync(ct);
        try
        {
            account.ChangePassword(request.NewPassword, new PasswordHash(_hasher.Hash(request.NewPassword)), policy, _clock);
        }
        catch (BusinessRuleViolationException ex)
        {
            return ex.ToError();
        }

        // Other devices must sign in again with the new password; the current session keeps working.
        await _sessions.InvalidateAllForAccountAsync(account.Id.Value, _user.SessionId, ct);
        return Result.Success();
    }
}
