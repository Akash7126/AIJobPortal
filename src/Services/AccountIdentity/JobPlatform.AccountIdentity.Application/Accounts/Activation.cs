using FluentValidation;
using JobPlatform.AccountIdentity.Application.Abstractions;
using JobPlatform.AccountIdentity.Domain.Accounts;
using JobPlatform.AccountIdentity.Domain.Common;
using JobPlatform.SharedKernel.Application.Abstractions;
using JobPlatform.SharedKernel.Application.Ports;
using JobPlatform.SharedKernel.Application.Results;
using JobPlatform.SharedKernel.Common.ValueObjects;
using JobPlatform.SharedKernel.Domain;
using JobPlatform.SharedKernel.Security;
using Microsoft.Extensions.Logging;

namespace JobPlatform.AccountIdentity.Application.Accounts;

// ---------------------------------------------------------------------- resend code (US-3.1.1-02)

public sealed record ResendActivationCodeCommand(Guid AccountId) : ICommand<Unit>;

public sealed class ResendActivationCodeValidator : AbstractValidator<ResendActivationCodeCommand>
{
    public ResendActivationCodeValidator() =>
        RuleFor(x => x.AccountId).NotEmpty().WithErrorCode("VAL.AccountId.Required");
}

internal sealed class ResendActivationCodeHandler : ICommandHandler<ResendActivationCodeCommand, Unit>
{
    private readonly IAccountRepository _accounts;
    private readonly IRateLimiter _limiter;
    private readonly ICurrentUser _user;
    private readonly IOtpHasher _otpHasher;
    private readonly ISecretGenerator _generator;
    private readonly IOtpSender _sender;
    private readonly TimeProvider _clock;
    private readonly ILogger<ResendActivationCodeHandler> _logger;

    public ResendActivationCodeHandler(IAccountRepository accounts, IRateLimiter limiter, ICurrentUser user, IOtpHasher otpHasher,
        ISecretGenerator generator, IOtpSender sender, TimeProvider clock, ILogger<ResendActivationCodeHandler> logger)
    {
        _accounts = accounts;
        _limiter = limiter;
        _user = user;
        _otpHasher = otpHasher;
        _generator = generator;
        _sender = sender;
        _clock = clock;
        _logger = logger;
    }

    public async Task<Result<Unit>> Handle(ResendActivationCodeCommand request, CancellationToken ct)
    {
        var account = await _accounts.GetByIdAsync(new AccountId(request.AccountId), ct);
        if (account is null)
        {
            return AccountErrors.NotFound;
        }

        var decision = await _limiter.HitAsync($"activation-code:{_user.SourceKey}:{account.Id}", 5, TimeSpan.FromMinutes(15), ct);
        if (!decision.Allowed)
        {
            return Error.TooManyRequests(ErrorCodes.RateLimited(account.ActorType), "Too many requests. Try again later.", decision.RetryAfter);
        }

        var code = _generator.GenerateOtp();
        account.IssueActivationChallenge(_otpHasher.Hash(code), _clock);
        try
        {
            await _sender.SendActivationCodeAsync(account.Mobile, code, _user.Language, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not deliver activation code for account {AccountId}", account.Id.Value);
        }

        return Result.Success();
    }
}

// ---------------------------------------------------------------------- activate (US-3.1.1-02, US-3.1.2-02)

/// <summary>A wrong or expired code still consumes an attempt, so the aggregate is saved even when the result is a failure.</summary>
public sealed record ActivateAccountCommand(Guid AccountId, string Code) : ICommand<Unit>, IPersistOnFailure;

public sealed class ActivateAccountValidator : AbstractValidator<ActivateAccountCommand>
{
    public ActivateAccountValidator()
    {
        RuleFor(x => x.AccountId).NotEmpty().WithErrorCode("VAL.AccountId.Required");
        RuleFor(x => x.Code).NotEmpty().WithErrorCode("VAL.Code.Required").DependentRules(() =>
            RuleFor(x => x.Code).Matches("^[0-9]{6}$").WithErrorCode("VAL.Code.Format"));
    }
}

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

// ---------------------------------------------------------------------- partner approval by MoL/PEF-authorised staff (US-3.1.3-01 AC-04)

public sealed record ApprovePartnerAccountCommand(Guid AccountId)
    : AdminAuthorized(Permissions.AccountsApprovePartner, ErrorCodes.AdminForbidden), ICommand<Unit>;

public sealed class ApprovePartnerAccountValidator : AbstractValidator<ApprovePartnerAccountCommand>
{
    public ApprovePartnerAccountValidator() =>
        RuleFor(x => x.AccountId).NotEmpty().WithErrorCode("VAL.AccountId.Required");
}

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
