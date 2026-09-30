using JobPlatform.AccountIdentity.Application.DTOs.Accounts;
using JobPlatform.AccountIdentity.Application.Interfaces;
using JobPlatform.AccountIdentity.Domain.Accounts;
using JobPlatform.AccountIdentity.Domain.Common;
using JobPlatform.AccountIdentity.Domain.Interfaces.Repositories;
using JobPlatform.SharedKernel.Application.Interfaces.Ports;
using JobPlatform.SharedKernel.Application.Results;
using JobPlatform.SharedKernel.Common.Enums;
using JobPlatform.SharedKernel.Common.ValueObjects;
using Microsoft.Extensions.Logging;

namespace JobPlatform.AccountIdentity.Application.Services.Accounts;

/// <summary>Orchestration common to the three registration commands. Business rules (uniqueness, password policy) live in the domain.</summary>
internal sealed class RegistrationWorkflow
{
    private readonly IPasswordPolicyRepository _policies;
    private readonly IPasswordHasher _hasher;
    private readonly AccountRegistrar _registrar;
    private readonly IAccountRepository _accounts;
    private readonly IOtpHasher _otpHasher;
    private readonly IApiSecretHasher _tokenHasher;
    private readonly ISecretGenerator _generator;
    private readonly IOtpSender _otpSender;
    private readonly IEmailVerificationSender _emailSender;
    private readonly ICurrentUser _user;
    private readonly TimeProvider _clock;
    private readonly ILogger<RegistrationWorkflow> _logger;

    public RegistrationWorkflow(IPasswordPolicyRepository policies, IPasswordHasher hasher, AccountRegistrar registrar, IAccountRepository accounts,
        IOtpHasher otpHasher, IApiSecretHasher tokenHasher, ISecretGenerator generator, IOtpSender otpSender, IEmailVerificationSender emailSender,
        ICurrentUser user, TimeProvider clock, ILogger<RegistrationWorkflow> logger)
    {
        _policies = policies;
        _hasher = hasher;
        _registrar = registrar;
        _accounts = accounts;
        _otpHasher = otpHasher;
        _tokenHasher = tokenHasher;
        _generator = generator;
        _otpSender = otpSender;
        _emailSender = emailSender;
        _user = user;
        _clock = clock;
        _logger = logger;
    }

    public async Task<Result<RegisteredAccountDto>> RegisterAsync(RegistrationInput input, CancellationToken ct)
    {
        var policy = await _policies.GetAsync(ct);
        policy.EnsureCompliant(input.Password);

        var mobile = MobileNumber.Create(input.Mobile);
        var email = Email.TryCreate(input.Email, out var parsedEmail) ? parsedEmail : null;
        var identityKey = ExternalIdentityKey.TryCreate(input.IdentityKey, out var key) ? key : null;
        var details = new RegistrationDetails(input.ActorType, input.DisplayName.Trim(), email, mobile, identityKey, input.Level,
            policy.PolicyVersion, input.RegistrationNumber?.Trim());

        var account = await _registrar.RegisterAsync(details, new PasswordHash(_hasher.Hash(input.Password)), ct);
        var language = input.PreferredLanguage == "ar" ? Language.Ar : _user.Language;

        DateTime? codeExpiry = null;
        string? otp = null;
        if (input.ActorType is ActorType.JobSeeker or ActorType.Employer)
        {
            otp = _generator.GenerateOtp();
            account.IssueActivationChallenge(_otpHasher.Hash(otp), _clock);
            codeExpiry = account.ActivationChallenge!.ExpiresAtUtc;
        }

        string? emailToken = null;
        if (email is not null)
        {
            emailToken = _generator.GenerateToken();
            account.IssueEmailVerification(_tokenHasher.Hash(emailToken), _clock);
        }

        _accounts.Add(account);

        // Delivery problems must not fail the registration: the user can request a new code (rate limited).
        if (otp is not null)
        {
            await TrySendAsync(() => _otpSender.SendActivationCodeAsync(mobile, otp, language, ct), "activation code", account.Id.Value);
        }

        if (email is not null && emailToken is not null)
        {
            await TrySendAsync(() => _emailSender.SendVerificationAsync(email, account.Id.Value, emailToken, language, ct), "e-mail verification", account.Id.Value);
        }

        return new RegisteredAccountDto(account.Id.Value, account.ActorType.ToString(), account.Standing.ToString(),
            otp is not null ? RegistrationNextStep.ActivateWithMobileCode : RegistrationNextStep.AwaitStaffApproval, codeExpiry);
    }

    private async Task TrySendAsync(Func<Task> send, string what, Guid accountId)
    {
        try
        {
            await send();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not deliver {Delivery} for account {AccountId}; the user can request a new one", what, accountId);
        }
    }
}
