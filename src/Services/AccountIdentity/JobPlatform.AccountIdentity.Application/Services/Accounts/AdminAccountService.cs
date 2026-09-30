using JobPlatform.AccountIdentity.Application.Interfaces;
using JobPlatform.AccountIdentity.Domain.Accounts;
using JobPlatform.AccountIdentity.Domain.Common;
using JobPlatform.AccountIdentity.Domain.Interfaces.Repositories;
using JobPlatform.SharedKernel.Application.Concurrency;
using JobPlatform.SharedKernel.Application.Interfaces.Ports;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.AccountIdentity.Application.Services.Accounts;

/// <summary>Logic shared by the admin account request handlers.</summary>
internal sealed class AdminAccountService
{
    private readonly IAccountRepository _accounts;
    private readonly ISessionStore _sessions;
    private readonly ICurrentUser _user;

    public AdminAccountService(IAccountRepository accounts, ISessionStore sessions, ICurrentUser user)
    {
        _accounts = accounts;
        _sessions = sessions;
        _user = user;
    }

    public Actor Admin => Actor.Administrator(_user.UserId!.Value);

    public async Task<Result<Unit>> Run(Guid accountId, Action<Account> action, bool invalidateSessions, string? ifMatch, CancellationToken ct)
    {
        var account = await _accounts.GetByIdAsync(new AccountId(accountId), ct);
        if (account is null)
        {
            return AccountErrors.NotFound;
        }

        if (!ETag.Matches(ifMatch, account.RowVersion))
        {
            return ConcurrencyErrors.PreconditionFailed;
        }

        action(account);
        if (invalidateSessions)
        {
            // Ban, deactivation and credential reset end live sessions explicitly (Redis key session:{id} is evicted on ban).
            await _sessions.InvalidateAllForAccountAsync(account.Id.Value, null, ct);
        }

        return Result.Success();
    }
}
