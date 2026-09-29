using FluentValidation.TestHelper;
using JobPlatform.AccountIdentity.Application.Commands.Accounts;
using JobPlatform.AccountIdentity.Application.Commands.Administration;
using JobPlatform.AccountIdentity.Application.Commands.ApiCredentials;
using JobPlatform.AccountIdentity.Application.Commands.Authentication;
using JobPlatform.AccountIdentity.Application.Commands.Consent;
using JobPlatform.AccountIdentity.Application.Queries.Accounts;
using JobPlatform.AccountIdentity.Application.Queries.Internal;
using JobPlatform.AccountIdentity.Application.Validators.Accounts;
using JobPlatform.AccountIdentity.Application.Validators.Administration;
using JobPlatform.AccountIdentity.Application.Validators.ApiCredentials;
using JobPlatform.AccountIdentity.Application.Validators.Authentication;
using JobPlatform.AccountIdentity.Application.Validators.Consent;
using JobPlatform.AccountIdentity.Application.Validators.Internal;
using JobPlatform.SharedKernel.Common.Enums;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using AppConsentOptions = JobPlatform.AccountIdentity.Application.Abstractions.ConsentOptions;

namespace JobPlatform.AccountIdentity.Application.UnitTests;

/// <summary>Malformed input is a validation error (400); strength/state rules belong to the domain, so validators never check them.</summary>
public class ValidatorTests
{
    private static readonly FakeTimeProvider Clock = AppKit.Clock();

    // ------------------------------------------------------------------ registration

    private static RegisterJobSeekerAccountCommand Seeker(string name = "Sara", string mobile = "+970591111111", string? email = "sara@example.com",
        string password = "Str0ngPass", string? language = null) => new(name, mobile, email, password, language, null);

    [Fact]
    public void RegisterJobSeeker_ValidRequest_HasNoErrors() => new RegisterJobSeekerValidator().TestValidate(Seeker()).ShouldNotHaveAnyValidationErrors();

    [Theory]
    [InlineData("", "VAL.FullName.Required")]
    [InlineData(null, "VAL.FullName.Required")]
    public void RegisterJobSeeker_NameRequired(string? name, string code) =>
        new RegisterJobSeekerValidator().TestValidate(Seeker(name: name!)).ShouldHaveValidationErrorFor(x => x.FullName).WithErrorCode(code);

    [Fact]
    public void RegisterJobSeeker_NameTooLong() =>
        new RegisterJobSeekerValidator().TestValidate(Seeker(name: new string('x', 201))).ShouldHaveValidationErrorFor(x => x.FullName).WithErrorCode("VAL.FullName.TooLong");

    [Theory]
    [InlineData("0591234567")]
    [InlineData("+97059")]
    [InlineData("phone")]
    [InlineData("+0591111111")]
    public void RegisterJobSeeker_MobileMustBeE164(string mobile) =>
        new RegisterJobSeekerValidator().TestValidate(Seeker(mobile: mobile)).ShouldHaveValidationErrorFor(x => x.Mobile).WithErrorCode("VAL.MobileNumber.Invalid");

    [Fact]
    public void RegisterJobSeeker_MobileRequired() =>
        new RegisterJobSeekerValidator().TestValidate(Seeker(mobile: "")).ShouldHaveValidationErrorFor(x => x.Mobile).WithErrorCode("VAL.MobileNumber.Required");

    [Fact]
    public void RegisterJobSeeker_EmailIsOptionalButMustBeValidWhenGiven()
    {
        var validator = new RegisterJobSeekerValidator();
        validator.TestValidate(Seeker(email: null)).ShouldNotHaveValidationErrorFor(x => x.Email);
        validator.TestValidate(Seeker(email: "")).ShouldNotHaveValidationErrorFor(x => x.Email);
        validator.TestValidate(Seeker(email: "not-an-email")).ShouldHaveValidationErrorFor(x => x.Email!).WithErrorCode("VAL.Email.Invalid");
        validator.TestValidate(Seeker(email: new string('a', 250) + "@x.com")).ShouldHaveValidationErrorFor(x => x.Email!);
    }

    [Fact]
    public void RegisterJobSeeker_PasswordPresenceAndLengthOnly_StrengthIsADomainPolicy()
    {
        var validator = new RegisterJobSeekerValidator();
        validator.TestValidate(Seeker(password: "")).ShouldHaveValidationErrorFor(x => x.Password).WithErrorCode("VAL.Password.Required");
        validator.TestValidate(Seeker(password: new string('a', 129))).ShouldHaveValidationErrorFor(x => x.Password).WithErrorCode("VAL.Password.MaxLength");
        validator.TestValidate(Seeker(password: "weak")).ShouldNotHaveValidationErrorFor(x => x.Password);
    }

    [Theory]
    [InlineData("ar", false)]
    [InlineData("en", false)]
    [InlineData("de", true)]
    public void RegisterJobSeeker_PreferredLanguageMustBeArOrEn(string language, bool invalid)
    {
        var result = new RegisterJobSeekerValidator().TestValidate(Seeker(language: language));

        if (invalid)
        {
            result.ShouldHaveValidationErrorFor(x => x.PreferredLanguage!).WithErrorCode("VAL.PreferredLanguage.Invalid");
        }
        else
        {
            result.ShouldNotHaveAnyValidationErrors();
        }
    }

    private static RegisterEmployerAccountCommand Employer(string company = "Acme", string email = "hr@acme.com", string mobile = "+970592222222",
        string companyId = "C-1", string registration = "R-1", int level = 1) =>
        new(company, email, mobile, companyId, registration, "Str0ngPass", level, null);

    [Fact]
    public void RegisterEmployer_ValidRequest_HasNoErrors() => new RegisterEmployerValidator().TestValidate(Employer()).ShouldNotHaveAnyValidationErrors();

    [Fact]
    public void RegisterEmployer_LevelOneFieldsAreAllRequired()
    {
        var validator = new RegisterEmployerValidator();
        validator.TestValidate(Employer(company: "")).ShouldHaveValidationErrorFor(x => x.CompanyName).WithErrorCode("VAL.CompanyName.Required");
        validator.TestValidate(Employer(company: new string('x', 201))).ShouldHaveValidationErrorFor(x => x.CompanyName).WithErrorCode("VAL.CompanyName.TooLong");
        validator.TestValidate(Employer(email: "")).ShouldHaveValidationErrorFor(x => x.Email).WithErrorCode("VAL.Email.Required");
        validator.TestValidate(Employer(email: "nope")).ShouldHaveValidationErrorFor(x => x.Email).WithErrorCode("VAL.Email.Invalid");
        validator.TestValidate(Employer(mobile: "123")).ShouldHaveValidationErrorFor(x => x.Mobile).WithErrorCode("VAL.MobileNumber.Invalid");
        validator.TestValidate(Employer(companyId: "")).ShouldHaveValidationErrorFor(x => x.CompanyId).WithErrorCode("VAL.CompanyId.Required");
        validator.TestValidate(Employer(companyId: new string('x', 129))).ShouldHaveValidationErrorFor(x => x.CompanyId).WithErrorCode("VAL.CompanyId.TooLong");
        validator.TestValidate(Employer(registration: "")).ShouldHaveValidationErrorFor(x => x.RegistrationNumber).WithErrorCode("VAL.RegistrationNumber.Required");
        validator.TestValidate(Employer(registration: new string('x', 65))).ShouldHaveValidationErrorFor(x => x.RegistrationNumber).WithErrorCode("VAL.RegistrationNumber.TooLong");
    }

    [Theory]
    [InlineData(1, false)]
    [InlineData(2, false)]
    [InlineData(0, true)]
    [InlineData(3, true)]
    [Trait("Story", "US-3.1.2-01")]
    [Trait("AC", "AC-02")]
    public void RegisterEmployer_LevelMustBeOneOrTwo(int level, bool invalid)
    {
        var result = new RegisterEmployerValidator().TestValidate(Employer(level: level));

        if (invalid)
        {
            result.ShouldHaveValidationErrorFor(x => x.Level).WithErrorCode("VAL.Level.Invalid");
        }
        else
        {
            result.ShouldNotHaveValidationErrorFor(x => x.Level);
        }
    }

    [Fact]
    public void RegisterPartner_RequiresOrganisationContactMobileAndIdentity()
    {
        var validator = new RegisterPartnerValidator();
        RegisterPartnerAccountCommand Partner(string org = "Jobs", string email = "it@jobs.com", string mobile = "+970593333333", string identity = "jobs.com") =>
            new(org, email, mobile, identity, "Str0ngPass", null);

        validator.TestValidate(Partner()).ShouldNotHaveAnyValidationErrors();
        validator.TestValidate(Partner(org: "")).ShouldHaveValidationErrorFor(x => x.OrganisationName).WithErrorCode("VAL.OrganisationName.Required");
        validator.TestValidate(Partner(email: "x")).ShouldHaveValidationErrorFor(x => x.ContactEmail).WithErrorCode("VAL.Email.Invalid");
        validator.TestValidate(Partner(mobile: "1")).ShouldHaveValidationErrorFor(x => x.Mobile).WithErrorCode("VAL.MobileNumber.Invalid");
        validator.TestValidate(Partner(identity: "")).ShouldHaveValidationErrorFor(x => x.Identity).WithErrorCode("VAL.Identity.Required");
        validator.TestValidate(Partner(identity: new string('x', 129))).ShouldHaveValidationErrorFor(x => x.Identity).WithErrorCode("VAL.Identity.TooLong");
    }

    // ------------------------------------------------------------------ activation

    [Theory]
    [InlineData("123456", false)]
    [InlineData("12345", true)]
    [InlineData("1234567", true)]
    [InlineData("12345a", true)]
    [InlineData("", true)]
    public void ActivateAccount_CodeMustBeSixDigits(string code, bool invalid)
    {
        var result = new ActivateAccountValidator().TestValidate(new ActivateAccountCommand(Guid.NewGuid(), code));

        if (invalid)
        {
            result.ShouldHaveValidationErrorFor(x => x.Code);
        }
        else
        {
            result.ShouldNotHaveAnyValidationErrors();
        }
    }

    [Fact]
    public void ActivateAccount_AccountIdRequired() =>
        new ActivateAccountValidator().TestValidate(new ActivateAccountCommand(Guid.Empty, "123456")).ShouldHaveValidationErrorFor(x => x.AccountId).WithErrorCode("VAL.AccountId.Required");

    [Fact]
    public void AccountIdCommands_RequireAnId()
    {
        new ResendActivationCodeValidator().TestValidate(new ResendActivationCodeCommand(Guid.Empty)).ShouldHaveValidationErrorFor(x => x.AccountId);
        new ApprovePartnerAccountValidator().TestValidate(new ApprovePartnerAccountCommand(Guid.Empty)).ShouldHaveValidationErrorFor(x => x.AccountId);
        new ApproveUserAccountValidator().TestValidate(new ApproveUserAccountCommand(Guid.Empty)).ShouldHaveValidationErrorFor(x => x.AccountId);
        new ResetCredentialsValidator().TestValidate(new ResetCredentialsCommand(Guid.Empty)).ShouldHaveValidationErrorFor(x => x.AccountId);
        new AssignRoleToAccountValidator().TestValidate(new AssignRoleToAccountCommand(Guid.Empty, Guid.Empty)).ShouldHaveValidationErrorFor(x => x.RoleId);
        new RemoveRoleFromAccountValidator().TestValidate(new RemoveRoleFromAccountCommand(Guid.Empty, Guid.Empty)).ShouldHaveValidationErrorFor(x => x.AccountId);
        new ResendActivationCodeValidator().TestValidate(new ResendActivationCodeCommand(Guid.NewGuid())).ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void BanAndDeactivate_RequireAReasonUpToFiveHundredCharacters()
    {
        new BanUserAccountValidator().TestValidate(new BanUserAccountCommand(Guid.NewGuid(), "")).ShouldHaveValidationErrorFor(x => x.Reason).WithErrorCode("VAL.Reason.Required");
        new BanUserAccountValidator().TestValidate(new BanUserAccountCommand(Guid.NewGuid(), new string('x', 501))).ShouldHaveValidationErrorFor(x => x.Reason).WithErrorCode("VAL.Reason.TooLong");
        new DeactivateUserAccountValidator().TestValidate(new DeactivateUserAccountCommand(Guid.NewGuid(), " ")).ShouldHaveValidationErrorFor(x => x.Reason);
        new DeactivateUserAccountValidator().TestValidate(new DeactivateUserAccountCommand(Guid.NewGuid(), "ok")).ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData("Deactivate", false)]
    [InlineData("Delete", false)]
    [InlineData("Explode", true)]
    public void RequestAccountDeactivation_KindMustBeKnown(string kind, bool invalid)
    {
        var result = new RequestAccountDeactivationValidator().TestValidate(new RequestAccountDeactivationCommand(Guid.NewGuid(), kind, "why"));

        if (invalid)
        {
            result.ShouldHaveValidationErrorFor(x => x.Kind).WithErrorCode("VAL.Kind.Invalid");
        }
        else
        {
            result.ShouldNotHaveAnyValidationErrors();
        }
    }

    [Fact]
    public void ListAccounts_ValidatesPagingAndFilters()
    {
        var validator = new ListAccountsValidator();
        validator.TestValidate(new ListAccountsQuery(null, "Active", "sara", 1, 50)).ShouldNotHaveAnyValidationErrors();
        validator.TestValidate(new ListAccountsQuery(null, null, null, 0, 20)).ShouldHaveValidationErrorFor(x => x.Page);
        validator.TestValidate(new ListAccountsQuery(null, null, null, 1, 101)).ShouldHaveValidationErrorFor(x => x.PageSize);
        validator.TestValidate(new ListAccountsQuery(null, "Weird", null)).ShouldHaveValidationErrorFor(x => x.Standing!).WithErrorCode("VAL.Standing.Invalid");
        validator.TestValidate(new ListAccountsQuery(null, null, new string('x', 101))).ShouldHaveValidationErrorFor(x => x.Search!).WithErrorCode("VAL.Search.TooLong");
    }

    // ------------------------------------------------------------------ authentication

    private static AuthenticateUserCommand Login(string username = "u@example.com", string? password = "pw", string mechanism = "password", string? mfa = null, string? emailCode = null) =>
        new(username, password, mechanism, null, mfa, emailCode);

    [Fact]
    public void Authenticate_PasswordMechanism_RequiresUsernameAndPassword()
    {
        var validator = new AuthenticateUserValidator();
        validator.TestValidate(Login()).ShouldNotHaveAnyValidationErrors();
        validator.TestValidate(Login(username: "")).ShouldHaveValidationErrorFor(x => x.Username).WithErrorCode("VAL.Username.Required");
        validator.TestValidate(Login(username: new string('x', 255))).ShouldHaveValidationErrorFor(x => x.Username).WithErrorCode("VAL.Username.TooLong");
        validator.TestValidate(Login(password: "")).ShouldHaveValidationErrorFor(x => x.Password).WithErrorCode("VAL.Password.Required");
        validator.TestValidate(Login(password: new string('x', 129))).ShouldHaveValidationErrorFor(x => x.Password).WithErrorCode("VAL.Password.MaxLength");
    }

    [Theory]
    [InlineData("password", false)]
    [InlineData("email-verification", false)]
    [InlineData("mfa", false)]
    [InlineData("sms", true)]
    [InlineData("", true)]
    [Trait("Story", "US-3.1.5-01")]
    [Trait("AC", "AC-02")]
    public void Authenticate_MechanismMustBeOneOfTheSupportedThree(string mechanism, bool invalid)
    {
        var command = Login(mechanism: mechanism, mfa: "123456", password: "pw");
        var result = new AuthenticateUserValidator().TestValidate(command with { Username = mechanism == "email-verification" ? "u@example.com" : "u" });

        if (invalid)
        {
            result.ShouldHaveValidationErrorFor(x => x.Mechanism).WithErrorCode("VAL.Mechanism.Invalid");
        }
        else
        {
            result.ShouldNotHaveValidationErrorFor(x => x.Mechanism);
        }
    }

    [Fact]
    public void Authenticate_MfaMechanism_RequiresASixDigitCode()
    {
        var validator = new AuthenticateUserValidator();
        validator.TestValidate(Login(mechanism: "mfa", mfa: "123456")).ShouldNotHaveAnyValidationErrors();
        validator.TestValidate(Login(mechanism: "mfa", mfa: null)).ShouldHaveValidationErrorFor(x => x.MfaCode).WithErrorCode("VAL.MfaCode.Required");
        validator.TestValidate(Login(mechanism: "mfa", mfa: "12")).ShouldHaveValidationErrorFor(x => x.MfaCode!).WithErrorCode("VAL.MfaCode.Format");
    }

    [Fact]
    public void Authenticate_EmailVerification_NeedsAnEmailUsername_AndASixDigitCodeWhenGiven()
    {
        var validator = new AuthenticateUserValidator();
        validator.TestValidate(Login("u@example.com", null, "email-verification")).ShouldNotHaveAnyValidationErrors();
        validator.TestValidate(Login("not-email", null, "email-verification")).ShouldHaveValidationErrorFor(x => x.Username).WithErrorCode("VAL.Email.Invalid");
        validator.TestValidate(Login("u@example.com", null, "email-verification", emailCode: "1")).ShouldHaveValidationErrorFor(x => x.EmailCode!).WithErrorCode("VAL.EmailCode.Format");
        validator.TestValidate(Login("u@example.com", null, "email-verification", emailCode: "123456")).ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void MfaAndRefreshAndEmailVerification_ValidateTheirInputs()
    {
        new VerifyMfaValidator().TestValidate(new VerifyMfaCommand("", "123456")).ShouldHaveValidationErrorFor(x => x.MfaToken);
        new VerifyMfaValidator().TestValidate(new VerifyMfaCommand("t", "abc")).ShouldHaveValidationErrorFor(x => x.Code);
        new VerifyMfaValidator().TestValidate(new VerifyMfaCommand("t", "123456")).ShouldNotHaveAnyValidationErrors();
        new BeginMfaEnrollmentValidator().TestValidate(new BeginMfaEnrollmentCommand("")).ShouldHaveValidationErrorFor(x => x.MfaToken);
        new RefreshSessionValidator().TestValidate(new RefreshSessionCommand("")).ShouldHaveValidationErrorFor(x => x.RefreshToken);
        new VerifyEmailValidator().TestValidate(new VerifyEmailCommand(Guid.Empty, "")).ShouldHaveValidationErrorFor(x => x.AccountId);
        new VerifyEmailValidator().TestValidate(new VerifyEmailCommand(Guid.NewGuid(), new string('x', 257))).ShouldHaveValidationErrorFor(x => x.Token);
        new VerifyEmailValidator().TestValidate(new VerifyEmailCommand(Guid.NewGuid(), "tok")).ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    [Trait("Story", "US-3.1.5-02")]
    [Trait("AC", "AC-02")]
    public void ChangePassword_NewMustDifferFromCurrent_AndFitTheLengthLimit_ButStrengthIsTheDomainsJob()
    {
        var validator = new ChangePasswordValidator();
        validator.TestValidate(new ChangePasswordCommand("Old1passw", "New1passw")).ShouldNotHaveAnyValidationErrors();
        validator.TestValidate(new ChangePasswordCommand("Same1passw", "Same1passw")).ShouldHaveValidationErrorFor(x => x.NewPassword).WithErrorCode("VAL.NewPassword.SameAsCurrent");
        validator.TestValidate(new ChangePasswordCommand("", "New1passw")).ShouldHaveValidationErrorFor(x => x.CurrentPassword);
        validator.TestValidate(new ChangePasswordCommand("Old1passw", "")).ShouldHaveValidationErrorFor(x => x.NewPassword).WithErrorCode("VAL.NewPassword.Required");
        validator.TestValidate(new ChangePasswordCommand("Old1passw", new string('x', 129))).ShouldHaveValidationErrorFor(x => x.NewPassword).WithErrorCode("VAL.NewPassword.MaxLength");
        validator.TestValidate(new ChangePasswordCommand("Old1passw", "weak")).ShouldNotHaveValidationErrorFor(x => x.NewPassword);
    }

    // ------------------------------------------------------------------ configuration

    [Theory]
    [InlineData(8, true, false, false, true)]
    [InlineData(128, false, true, false, true)]
    [InlineData(7, true, true, true, false)]
    [InlineData(129, true, true, true, false)]
    [InlineData(10, false, false, false, false)]
    [Trait("Story", "US-3.1.5-02")]
    [Trait("AC", "AC-03")]
    public void ConfigurePasswordPolicy_MinLengthRangeAndAtLeastOneCharacterClass(int min, bool upper, bool lower, bool digit, bool valid) =>
        new ConfigurePasswordPolicyValidator().TestValidate(new ConfigurePasswordPolicyCommand(min, upper, lower, digit)).IsValid.Should().Be(valid);

    [Fact]
    public void ConfigurePasswordPolicy_ReportsWhichRuleFailed()
    {
        var validator = new ConfigurePasswordPolicyValidator();
        validator.TestValidate(new ConfigurePasswordPolicyCommand(4, true, true, true)).ShouldHaveValidationErrorFor(x => x.MinLength).WithErrorCode("VAL.MinLength.Range");
        validator.TestValidate(new ConfigurePasswordPolicyCommand(10, false, false, false)).Errors.Should().Contain(e => e.ErrorCode == "VAL.CharacterClasses.Required");
        validator.TestValidate(new ConfigurePasswordPolicyCommand(10, true, false, false)).ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData(4, true)]
    [InlineData(5, false)]
    [InlineData(480, false)]
    [InlineData(481, true)]
    public void ConfigureSessionTimeout_MustBeFiveToFourHundredEightyMinutes(int minutes, bool invalid)
    {
        var result = new ConfigureSessionTimeoutValidator().TestValidate(new ConfigureSessionTimeoutCommand(minutes));

        if (invalid)
        {
            result.ShouldHaveValidationErrorFor(x => x.IdleTimeoutMinutes).WithErrorCode("VAL.IdleTimeoutMinutes.Range");
        }
        else
        {
            result.ShouldNotHaveAnyValidationErrors();
        }
    }

    [Fact]
    public void GrantAndRevokePermission_RequireARoleAndAPermission()
    {
        new GrantPermissionValidator().TestValidate(new GrantPermissionCommand(Guid.Empty, "")).ShouldHaveValidationErrorFor(x => x.RoleId);
        new GrantPermissionValidator().TestValidate(new GrantPermissionCommand(Guid.NewGuid(), "")).ShouldHaveValidationErrorFor(x => x.Permission);
        new RevokePermissionValidator().TestValidate(new RevokePermissionCommand(Guid.NewGuid(), new string('x', 101))).ShouldHaveValidationErrorFor(x => x.Permission);
        new RevokePermissionValidator().TestValidate(new RevokePermissionCommand(Guid.NewGuid(), "jobs.browse")).ShouldNotHaveAnyValidationErrors();
    }

    // ------------------------------------------------------------------ API credentials

    private static IssueApiCredentialValidator IssueValidator() => new(Clock);

    [Fact]
    public void IssueApiCredential_NoControls_IsValid_BecauseDefaultsApply() =>
        IssueValidator().TestValidate(new IssueApiCredentialCommand(null, null, null, null)).ShouldNotHaveAnyValidationErrors();

    [Theory]
    [InlineData("203.0.113.5", true)]
    [InlineData("203.0.113.0/24", true)]
    [InlineData("2001:db8::/32", true)]
    [InlineData("999.1.1.1", false)]
    [InlineData("10.0.0.0/99", false)]
    [InlineData("", false)]
    [Trait("Story", "US-3.1.3-02")]
    [Trait("AC", "AC-02")]
    public void IssueApiCredential_IpWhitelistEntriesMustBeAddressesOrCidr(string entry, bool valid)
    {
        var result = IssueValidator().TestValidate(new IssueApiCredentialCommand(new[] { entry }, null, null, null));

        if (valid)
        {
            result.ShouldNotHaveAnyValidationErrors();
        }
        else
        {
            result.Errors.Should().Contain(e => e.ErrorCode == "VAL.IpWhitelist.Invalid");
        }
    }

    [Fact]
    [Trait("Story", "US-3.1.3-02")]
    [Trait("AC", "AC-02")]
    public void IssueApiCredential_LimitsMustBePositive_AndExpiryFutureWithinMaxLifetime()
    {
        var now = Clock.GetUtcNow().UtcDateTime;
        var validator = IssueValidator();

        validator.TestValidate(new IssueApiCredentialCommand(null, 0, null, null)).Errors.Should().Contain(e => e.ErrorCode == "VAL.MaxRequests.Invalid" && e.PropertyName == "MaxRequests");
        validator.TestValidate(new IssueApiCredentialCommand(null, null, -5, null)).Errors.Should().Contain(e => e.ErrorCode == "VAL.PeriodSeconds.Invalid");
        validator.TestValidate(new IssueApiCredentialCommand(null, null, null, now.AddDays(-1))).Errors.Should().Contain(e => e.ErrorCode == "VAL.ExpiresAtUtc.OutOfRange");
        validator.TestValidate(new IssueApiCredentialCommand(null, null, null, now.AddDays(731))).Errors.Should().Contain(e => e.ErrorCode == "VAL.ExpiresAtUtc.OutOfRange");
        validator.TestValidate(new IssueApiCredentialCommand(null, 10, 60, now.AddDays(30))).ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void RevokeApiCredential_RequiresAnId() =>
        new RevokeApiCredentialValidator().TestValidate(new RevokeApiCredentialCommand(Guid.Empty)).ShouldHaveValidationErrorFor(x => x.ApiCredentialId);

    [Theory]
    [InlineData("client_credentials", "id", "secret", true)]
    [InlineData("password", "id", "secret", false)]
    [InlineData("client_credentials", "", "secret", false)]
    [InlineData("client_credentials", "id", "", false)]
    public void AuthenticateApiClient_RequiresClientCredentialsGrantAndBothCredentials(string grant, string id, string secret, bool valid)
    {
        var result = new AuthenticateApiClientValidator().TestValidate(new AuthenticateApiClientCommand(grant, id, secret));

        (result.IsValid).Should().Be(valid);
    }

    [Fact]
    public void AuthenticateApiClient_FieldsAreLengthLimited()
    {
        var validator = new AuthenticateApiClientValidator();
        validator.TestValidate(new AuthenticateApiClientCommand("client_credentials", new string('x', 129), "s")).ShouldHaveValidationErrorFor(x => x.ClientId).WithErrorCode("VAL.ClientId.TooLong");
        validator.TestValidate(new AuthenticateApiClientCommand("client_credentials", "i", new string('x', 257))).ShouldHaveValidationErrorFor(x => x.ClientSecret).WithErrorCode("VAL.ClientSecret.TooLong");
        validator.TestValidate(new AuthenticateApiClientCommand("password", "i", "s")).ShouldHaveValidationErrorFor(x => x.GrantType).WithErrorCode("VAL.GrantType.Unsupported");
    }

    // ------------------------------------------------------------------ consent & internal

    private static RecordPrivacyConsentValidator ConsentValidator() =>
        new(Options.Create(new AppConsentOptions { CurrentPolicyVersion = "2026-01" }));

    [Fact]
    [Trait("Story", "US-4.1-04")]
    [Trait("AC", "AC-05")]
    public void RecordConsent_PolicyVersionMustBeTheCurrentOne()
    {
        var validator = ConsentValidator();
        validator.TestValidate(new RecordPrivacyConsentCommand(null, "2026-01", true, false, false, "ar", null)).ShouldNotHaveAnyValidationErrors();
        validator.TestValidate(new RecordPrivacyConsentCommand(null, "", true, false, false, null, null)).ShouldHaveValidationErrorFor(x => x.PolicyVersion).WithErrorCode("VAL.PolicyVersion.Required");
        validator.TestValidate(new RecordPrivacyConsentCommand(null, "2020-01", true, false, false, null, null)).ShouldHaveValidationErrorFor(x => x.PolicyVersion).WithErrorCode("VAL.PolicyVersion.NotCurrent");
    }

    [Fact]
    public void RecordConsent_LocaleAndGuestIdAreChecked()
    {
        var validator = ConsentValidator();
        validator.TestValidate(new RecordPrivacyConsentCommand(null, "2026-01", false, false, false, "xx", null)).ShouldHaveValidationErrorFor(x => x.Locale!).WithErrorCode("VAL.Locale.Invalid");
        validator.TestValidate(new RecordPrivacyConsentCommand(Guid.Empty, "2026-01", false, false, false, null, null)).Errors.Should().Contain(e => e.ErrorCode == "VAL.GuestId.Invalid");
    }

    [Fact]
    public void ListAccessLog_ValidatesPagingAndTheDateRange()
    {
        var validator = new ListAccessLogValidator();
        validator.TestValidate(new ListAccessLogQuery(null, null, null)).ShouldNotHaveAnyValidationErrors();
        validator.TestValidate(new ListAccessLogQuery(null, null, null, 0, 10)).ShouldHaveValidationErrorFor(x => x.Page);
        validator.TestValidate(new ListAccessLogQuery(null, null, null, 1, 101)).ShouldHaveValidationErrorFor(x => x.PageSize);
        validator.TestValidate(new ListAccessLogQuery(DateTime.UtcNow, DateTime.UtcNow.AddDays(-1), null)).Errors.Should().Contain(e => e.ErrorCode == "VAL.Range.Invalid");
    }

    [Fact]
    public void CheckPermission_RequiresAccountAndPermission()
    {
        new CheckPermissionValidator().TestValidate(new CheckPermissionQuery(Guid.Empty, "")).Errors.Should().HaveCountGreaterThanOrEqualTo(2);
        new CheckPermissionValidator().TestValidate(new CheckPermissionQuery(Guid.NewGuid(), "jobs.browse")).ShouldNotHaveAnyValidationErrors();
        _ = ActorType.System;
    }
}
