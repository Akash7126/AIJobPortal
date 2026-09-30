using JobPlatform.AccountIdentity.Application.Abstractions;
using JobPlatform.AccountIdentity.Application.Commands.Accounts;
using JobPlatform.AccountIdentity.Application.Interfaces;
using JobPlatform.AccountIdentity.Domain.Common;
using JobPlatform.AccountIdentity.Domain.Interfaces.Repositories;
using JobPlatform.SharedKernel.Application.Interfaces.Cqrs;
using JobPlatform.SharedKernel.Application.Interfaces.Ports;
using JobPlatform.SharedKernel.Application.Results;
using JobPlatform.SharedKernel.Domain;

namespace JobPlatform.AccountIdentity.Application.Handlers.Accounts;

internal sealed class ActivateAccountHandler : ICommandHandler<ActivateAccountCommand, Unit>
{
    private readonly IAccountRepository _accounts;
    private readonly IRateLimiter _limiter;
    private readonly ICurrentUser _user;
    private readonly IOtpHasher _otpHasher;
    private readonly IAccessLog _accessLog;
    private readonly TimeProvider _clock;

    public ActivateAccountHandler(IAccountRepository accounts, IRateLimiter limiter, ICurrentUser user, IOtpHasher otpHasher, IAccessLog accessLog,
        TimeProvider clock)
    {
        _accounts = accounts;
        _limiter = limiter;
        _user = user;
        _otpHasher = otpHasher;
        _accessLog = accessLog;
        _clock = clock;
    }

    public async Task<Result<Unit>> Handle(ActivateAccountCommand request, CancellationToken ct)
    {
        var account = await _accounts.GetByIdAsync(new AccountId(request.AccountId), ct);
        if (account is null)
        {
            return AccountErrors.NotFound;
        }

        // Source-level throttle (5 / 15 min) on top of the aggregate's per-challenge attempt cap.
        var decision = await _limiter.HitAsync($"activation:{_user.SourceKey}:{account.Id}", 5, TimeSpan.FromMinutes(15), ct);
        if (!decision.Allowed)
        {
            return Error.TooManyRequests(ErrorCodes.RateLimited(account.ActorType), "Too many activation attempts. Try again later.", decision.RetryAfter);
        }

        try
        {
            account.Activate(request.Code, _otpHasher, _clock);
        }
        catch (BusinessRuleViolationException ex)
        {
            await _accessLog.AppendAsync(new AccessLogEntry(_clock.GetUtcNow().UtcDateTime, account.Id.Value, "account.activate", "account", "Deny", ex.Code, _user.IpAddress), ct);
            return ex.ToError();
        }

        await _accessLog.AppendAsync(new AccessLogEntry(_clock.GetUtcNow().UtcDateTime, account.Id.Value, "account.activate", "account", "Allow", null, _user.IpAddress), ct);
        return Result.Success();
    }
}
