using JobPlatform.AccountIdentity.Application.Abstractions;
using JobPlatform.AccountIdentity.Application.Commands.Authentication;
using JobPlatform.AccountIdentity.Application.DTOs.Authentication;
using JobPlatform.AccountIdentity.Application.Interfaces;
using JobPlatform.AccountIdentity.Application.Security;
using JobPlatform.AccountIdentity.Domain.Common;
using JobPlatform.AccountIdentity.Domain.Interfaces.Repositories;
using JobPlatform.SharedKernel.Application.Interfaces.Cqrs;
using JobPlatform.SharedKernel.Application.Interfaces.Ports;
using JobPlatform.SharedKernel.Application.Results;
using JobPlatform.SharedKernel.Domain;

namespace JobPlatform.AccountIdentity.Application.Handlers.Authentication;

internal sealed class VerifyMfaHandler : ICommandHandler<VerifyMfaCommand, AuthenticationResultDto>
{
    private readonly IMfaChallengeStore _challenges;
    private readonly IAccountRepository _accounts;
    private readonly IMfaService _mfa;
    private readonly SessionIssuer _issuer;
    private readonly IAccessLog _accessLog;
    private readonly ICurrentUser _user;
    private readonly TimeProvider _clock;

    public VerifyMfaHandler(IMfaChallengeStore challenges, IAccountRepository accounts, IMfaService mfa, SessionIssuer issuer, IAccessLog accessLog,
        ICurrentUser user, TimeProvider clock)
    {
        _challenges = challenges;
        _accounts = accounts;
        _mfa = mfa;
        _issuer = issuer;
        _accessLog = accessLog;
        _user = user;
        _clock = clock;
    }

    public async Task<Result<AuthenticationResultDto>> Handle(VerifyMfaCommand request, CancellationToken ct)
    {
        var accountId = await _challenges.GetAccountIdAsync(request.MfaToken, ct);
        var account = accountId is null ? null : await _accounts.GetByIdAsync(new AccountId(accountId.Value), ct);
        if (account is null)
        {
            return LoginErrors.InvalidCredentials;
        }

        if (account.MfaSecret is null)
        {
            return new BusinessRuleViolationException(Domain.Accounts.AccountRuleCodes.MfaNotStarted, "Multi-factor enrolment has not been started.",
                ErrorCodes.AdminStateConflict, BusinessRuleKind.Conflict).ToError();
        }

        var now = _clock.GetUtcNow().UtcDateTime;
        var matchedStep = _mfa.FindMatchingTimeStep(_mfa.Unprotect(account.MfaSecret), request.Code, now);
        var attempt = account.AttemptMfa(matchedStep, _clock);
        if (!attempt.IsSuccess)
        {
            await _accessLog.AppendAsync(new AccessLogEntry(now, account.Id.Value, "auth.mfa", "auth", "Deny", attempt.RuleCode ?? "invalid-mfa-code", _user.IpAddress), ct);
            return LoginErrors.From(attempt, now);
        }

        account.ConfirmMfaEnrollment(_clock);
        await _challenges.DeleteAsync(request.MfaToken, ct);
        await _accessLog.AppendAsync(new AccessLogEntry(now, account.Id.Value, "auth.mfa", "auth", "Allow", null, _user.IpAddress), ct);
        return await _issuer.IssueAsync(account, mfaVerified: true, ct);
    }
}
