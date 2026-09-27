using FluentValidation;
using JobPlatform.AccountIdentity.Application.Abstractions;
using JobPlatform.AccountIdentity.Domain.Accounts;
using JobPlatform.AccountIdentity.Domain.Common;
using JobPlatform.SharedKernel.Application.Abstractions;
using JobPlatform.SharedKernel.Application.Ports;
using JobPlatform.SharedKernel.Application.Results;
using JobPlatform.SharedKernel.Common.Enums;
using JobPlatform.SharedKernel.Common.ValueObjects;
using Microsoft.Extensions.Logging;

namespace JobPlatform.AccountIdentity.Application.Accounts;

public sealed record RegisteredAccountDto(Guid AccountId, string ActorType, string Standing, string NextStep, DateTime? ActivationCodeExpiresAtUtc);

public static class RegistrationNextStep
{
    public const string ActivateWithMobileCode = "ActivateWithMobileCode";
    public const string AwaitStaffApproval = "AwaitStaffApproval";
}

// ---------------------------------------------------------------------- commands (US-3.1.1-01, US-3.1.2-01, US-3.1.3-01)

public sealed record RegisterJobSeekerAccountCommand(string FullName, string Mobile, string? Email, string Password, string? PreferredLanguage,
    string? IdempotencyKey) : ICommand<RegisteredAccountDto>, IIdempotentCommand, IRateLimitedRequest, IConflictAwareCommand
{
    public string RateLimitScope => "registration:job-seeker";
    public string RateLimitedErrorCode => ErrorCodes.JobSeekerRateLimited;
    public string UniqueViolationErrorCode => ErrorCodes.JobSeekerDuplicate;
}

/// <param name="Level">Registration level 1 (company identity) or 2; Level 2 details themselves belong to BC-05.</param>
public sealed record RegisterEmployerAccountCommand(string CompanyName, string Email, string Mobile, string CompanyId, string RegistrationNumber,
    string Password, int Level, string? IdempotencyKey) : ICommand<RegisteredAccountDto>, IIdempotentCommand, IRateLimitedRequest, IConflictAwareCommand
{
    public string RateLimitScope => "registration:employer";
    public string RateLimitedErrorCode => ErrorCodes.EmployerRateLimited;
    public string UniqueViolationErrorCode => ErrorCodes.EmployerDuplicate;
}

public sealed record RegisterPartnerAccountCommand(string OrganisationName, string ContactEmail, string Mobile, string Identity, string Password,
    string? IdempotencyKey) : ICommand<RegisteredAccountDto>, IIdempotentCommand, IRateLimitedRequest, IConflictAwareCommand
{
    public string RateLimitScope => "registration:partner";
    public string RateLimitedErrorCode => ErrorCodes.AuthRateLimited;
    public string UniqueViolationErrorCode => ErrorCodes.PartnerDuplicate;
}

// ---------------------------------------------------------------------- validators (malformed input only; strength is a domain policy)

public sealed class RegisterJobSeekerValidator : AbstractValidator<RegisterJobSeekerAccountCommand>
{
    public RegisterJobSeekerValidator()
    {
        RuleFor(x => x.FullName).NotEmpty().WithErrorCode("VAL.FullName.Required")
            .MaximumLength(200).WithErrorCode("VAL.FullName.TooLong");
        RuleFor(x => x.Mobile).NotEmpty().WithErrorCode("VAL.MobileNumber.Required").DependentRules(() =>
            RuleFor(x => x.Mobile).ValidMobile());
        When(x => !string.IsNullOrWhiteSpace(x.Email), () =>
            RuleFor(x => x.Email!).ValidEmail());
        RuleFor(x => x.Password).NotEmpty().WithErrorCode("VAL.Password.Required")
            .MaximumLength(128).WithErrorCode("VAL.Password.MaxLength");
        When(x => !string.IsNullOrWhiteSpace(x.PreferredLanguage), () =>
            RuleFor(x => x.PreferredLanguage!).Must(l => l is "ar" or "en").WithErrorCode("VAL.PreferredLanguage.Invalid"));
    }
}

public sealed class RegisterEmployerValidator : AbstractValidator<RegisterEmployerAccountCommand>
{
    public RegisterEmployerValidator()
    {
        RuleFor(x => x.CompanyName).NotEmpty().WithErrorCode("VAL.CompanyName.Required")
            .MaximumLength(200).WithErrorCode("VAL.CompanyName.TooLong");
        RuleFor(x => x.Email).NotEmpty().WithErrorCode("VAL.Email.Required").DependentRules(() =>
            RuleFor(x => x.Email).ValidEmail());
        RuleFor(x => x.Mobile).NotEmpty().WithErrorCode("VAL.MobileNumber.Required").DependentRules(() =>
            RuleFor(x => x.Mobile).ValidMobile());
        RuleFor(x => x.CompanyId).NotEmpty().WithErrorCode("VAL.CompanyId.Required")
            .MaximumLength(ExternalIdentityKey.MaxLength).WithErrorCode("VAL.CompanyId.TooLong");
        RuleFor(x => x.RegistrationNumber).NotEmpty().WithErrorCode("VAL.RegistrationNumber.Required")
            .MaximumLength(64).WithErrorCode("VAL.RegistrationNumber.TooLong");
        RuleFor(x => x.Level).Must(l => l is 1 or 2).WithErrorCode("VAL.Level.Invalid");
        RuleFor(x => x.Password).NotEmpty().WithErrorCode("VAL.Password.Required")
            .MaximumLength(128).WithErrorCode("VAL.Password.MaxLength");
    }
}

public sealed class RegisterPartnerValidator : AbstractValidator<RegisterPartnerAccountCommand>
{
    public RegisterPartnerValidator()
    {
        RuleFor(x => x.OrganisationName).NotEmpty().WithErrorCode("VAL.OrganisationName.Required")
            .MaximumLength(200).WithErrorCode("VAL.OrganisationName.TooLong");
        RuleFor(x => x.ContactEmail).NotEmpty().WithErrorCode("VAL.Email.Required").DependentRules(() =>
            RuleFor(x => x.ContactEmail).ValidEmail());
        RuleFor(x => x.Mobile).NotEmpty().WithErrorCode("VAL.MobileNumber.Required").DependentRules(() =>
            RuleFor(x => x.Mobile).ValidMobile());
        RuleFor(x => x.Identity).NotEmpty().WithErrorCode("VAL.Identity.Required")
            .MaximumLength(ExternalIdentityKey.MaxLength).WithErrorCode("VAL.Identity.TooLong");
        RuleFor(x => x.Password).NotEmpty().WithErrorCode("VAL.Password.Required")
            .MaximumLength(128).WithErrorCode("VAL.Password.MaxLength");
    }
}

// ---------------------------------------------------------------------- shared workflow + handlers

internal sealed record RegistrationInput(ActorType ActorType, string DisplayName, string Mobile, string? Email, string? IdentityKey,
    string? RegistrationNumber, RegistrationLevel Level, string Password, string? PreferredLanguage);

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

internal sealed class RegisterJobSeekerAccountHandler : ICommandHandler<RegisterJobSeekerAccountCommand, RegisteredAccountDto>
{
    private readonly RegistrationWorkflow _workflow;

    public RegisterJobSeekerAccountHandler(RegistrationWorkflow workflow) => _workflow = workflow;

    public Task<Result<RegisteredAccountDto>> Handle(RegisterJobSeekerAccountCommand r, CancellationToken ct) =>
        _workflow.RegisterAsync(new RegistrationInput(ActorType.JobSeeker, r.FullName, r.Mobile, r.Email, null, null, RegistrationLevel.Level1,
            r.Password, r.PreferredLanguage), ct);
}

internal sealed class RegisterEmployerAccountHandler : ICommandHandler<RegisterEmployerAccountCommand, RegisteredAccountDto>
{
    private readonly RegistrationWorkflow _workflow;

    public RegisterEmployerAccountHandler(RegistrationWorkflow workflow) => _workflow = workflow;

    public Task<Result<RegisteredAccountDto>> Handle(RegisterEmployerAccountCommand r, CancellationToken ct) =>
        _workflow.RegisterAsync(new RegistrationInput(ActorType.Employer, r.CompanyName, r.Mobile, r.Email, r.CompanyId, r.RegistrationNumber,
            r.Level == 2 ? RegistrationLevel.Level2 : RegistrationLevel.Level1, r.Password, null), ct);
}

internal sealed class RegisterPartnerAccountHandler : ICommandHandler<RegisterPartnerAccountCommand, RegisteredAccountDto>
{
    private readonly RegistrationWorkflow _workflow;

    public RegisterPartnerAccountHandler(RegistrationWorkflow workflow) => _workflow = workflow;

    public Task<Result<RegisteredAccountDto>> Handle(RegisterPartnerAccountCommand r, CancellationToken ct) =>
        _workflow.RegisterAsync(new RegistrationInput(ActorType.ExternalJobSite, r.OrganisationName, r.Mobile, r.ContactEmail, r.Identity, null,
            RegistrationLevel.Level1, r.Password, null), ct);
}
