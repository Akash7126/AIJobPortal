using JobPlatform.AccountIdentity.Application.Commands.Authentication;
using JobPlatform.AccountIdentity.Application.DTOs.Authentication;
using JobPlatform.AccountIdentity.Application.Interfaces;
using JobPlatform.AccountIdentity.Domain.Common;
using JobPlatform.AccountIdentity.Domain.Interfaces.Repositories;
using JobPlatform.SharedKernel.Application.Interfaces.Cqrs;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.AccountIdentity.Application.Handlers.Authentication;

internal sealed class BeginMfaEnrollmentHandler : ICommandHandler<BeginMfaEnrollmentCommand, MfaEnrollmentDto>
{
    private readonly IMfaChallengeStore _challenges;
    private readonly IAccountRepository _accounts;
    private readonly IMfaService _mfa;

    public BeginMfaEnrollmentHandler(IMfaChallengeStore challenges, IAccountRepository accounts, IMfaService mfa)
    {
        _challenges = challenges;
        _accounts = accounts;
        _mfa = mfa;
    }

    public async Task<Result<MfaEnrollmentDto>> Handle(BeginMfaEnrollmentCommand request, CancellationToken ct)
    {
        var accountId = await _challenges.GetAccountIdAsync(request.MfaToken, ct);
        var account = accountId is null ? null : await _accounts.GetByIdAsync(new AccountId(accountId.Value), ct);
        if (account is null)
        {
            return LoginErrors.InvalidCredentials;
        }

        var secret = _mfa.GenerateSecret();
        account.BeginMfaEnrollment(_mfa.Protect(secret));
        var label = account.Email?.Value ?? account.Mobile.Value;
        return new MfaEnrollmentDto(secret, _mfa.BuildProvisioningUri("JobPlatform", label, secret));
    }
}
