using JobPlatform.AccountIdentity.Application.Commands.Accounts;
using JobPlatform.AccountIdentity.Application.Interfaces;
using JobPlatform.AccountIdentity.Domain.Common;
using JobPlatform.AccountIdentity.Domain.Interfaces.Repositories;
using JobPlatform.SharedKernel.Application.Interfaces.Cqrs;
using JobPlatform.SharedKernel.Application.Interfaces.Ports;
using JobPlatform.SharedKernel.Application.Results;
using Microsoft.Extensions.Logging;

namespace JobPlatform.AccountIdentity.Application.Handlers.Accounts;

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
