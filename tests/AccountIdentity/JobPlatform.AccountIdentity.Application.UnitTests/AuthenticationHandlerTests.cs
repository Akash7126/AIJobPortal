using JobPlatform.AccountIdentity.Application.Abstractions;
using JobPlatform.AccountIdentity.Application.Authentication;
using JobPlatform.AccountIdentity.Application.Security;
using JobPlatform.AccountIdentity.Domain.Accounts;
using JobPlatform.AccountIdentity.Domain.Common;
using JobPlatform.SharedKernel.Application.Ports;
using JobPlatform.SharedKernel.Application.Results;
using JobPlatform.SharedKernel.Common.Enums;
using JobPlatform.SharedKernel.Domain;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;

namespace JobPlatform.AccountIdentity.Application.UnitTests;

public class AuthenticationHandlerTests
{
    private readonly FakeTimeProvider _clock = AppKit.Clock();
    private readonly InMemoryAccounts _accounts = new();
    private readonly FakeHasher _hasher = new();
    private readonly InMemorySessionStore _sessions = new();
    private readonly InMemoryMfaChallenges _challenges = new();
    private readonly InMemoryLoginCodes _loginCodes = new();
    private readonly RecordingAccessLog _log = new();
    private readonly IMfaService _mfa = Substitute.For<IMfaService>();
    private readonly IEmailVerificationSender _email = Substitute.For<IEmailVerificationSender>();
    private readonly IAccessTokenService _tokens = AppKit.Tokens();

    public AuthenticationHandlerTests()
    {
        _mfa.Unprotect(Arg.Any<string>()).Returns(ci => ci.Arg<string>().Replace("protected:", string.Empty));
        // The fake authenticator accepts "654321" and reports the 30 s step of the (fake) clock, like the real TOTP service.
        _mfa.FindMatchingTimeStep(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<DateTime>())
            .Returns(ci => ci.ArgAt<string>(1) == "654321" ? (long?)(ci.ArgAt<DateTime>(2).Ticks / (TimeSpan.TicksPerSecond * 30)) : null);
        _mfa.GenerateSecret().Returns("SEED");
        _mfa.Protect(Arg.Any<string>()).Returns(ci => "protected:" + ci.Arg<string>());
        _mfa.BuildProvisioningUri(default!, default!, default!).ReturnsForAnyArgs("otpauth://totp/x?secret=SEED");
    }

    private SessionIssuer Issuer() =>
        new(AppKit.TimeoutRepo(_clock), _sessions, _tokens, _hasher, new FakeGenerator(), _clock);

    private AuthenticateUserHandler Login() => new(_accounts, _hasher, _hasher, _loginCodes, _email, new FakeGenerator { Otp = "111222" }, _mfa, _challenges,
        Issuer(), _log, AppKit.User(), _clock, NullLogger<AuthenticateUserHandler>.Instance);

    private static AuthenticateUserCommand PasswordLogin(string username, string password = AppKit.Password) =>
        new(username, password, LoginMechanisms.Password, null, null, null);

    private Account ActiveUser(string mobile = "+970591111111", string? email = "u@example.com")
    {
        var account = AppKit.Active(_clock, mobile: mobile, email: email);
        _accounts.Add(account);
        return account;
    }

    private Account Administrator()
    {
        var admin = Account.CreateAdministrator("Admin", JobPlatform.SharedKernel.Common.ValueObjects.Email.Create("admin@x.com"),
            JobPlatform.SharedKernel.Common.ValueObjects.MobileNumber.Create("+970592222222"), new PasswordHash("h:" + AppKit.Password), 1, _clock);
        _accounts.Add(admin);
        return admin;
    }

    [Fact]
    [Trait("Story", "US-3.1.5-01")]
    [Trait("AC", "AC-01")]
    public async Task Login_WithValidCredentials_CreatesASessionAndReturnsTokens()
    {
        var account = ActiveUser();

        var result = await Login().Handle(PasswordLogin("+970591111111"), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be(AuthenticationStatus.Authenticated);
        var pair = result.Value.Tokens!;
        pair.AccessToken.Should().Be("jwt");
        var session = _sessions.Sessions.Values.Should().ContainSingle().Which;
        session.AccountId.Should().Be(account.Id.Value);
        session.IdleTimeoutMinutes.Should().Be(30, "captured from the setting at creation");
        pair.RefreshToken.Should().StartWith(session.SessionId.ToString("N") + ".");
        session.RefreshTokenHash.Should().StartWith("h:").And.NotBe(pair.RefreshToken);
        _log.Entries.Should().ContainSingle(e => e.Action == "auth.login" && e.Decision == "Allow");
        _tokens.Received(1).IssueUserToken(Arg.Is<UserTokenRequest>(r => r.AccountId == account.Id.Value && r.ActorType == ActorType.JobSeeker && !r.MfaVerified));
    }

    [Fact]
    [Trait("Story", "US-3.1.5-01")]
    [Trait("AC", "AC-04")]
    public async Task Login_UnknownUserAndWrongPassword_ProduceTheIdenticalError_AndUnknownUsersStillCostAHash()
    {
        ActiveUser();

        var unknown = AppKit.ErrorOf(await Login().Handle(PasswordLogin("+970599999999"), default));
        var wrong = AppKit.ErrorOf(await Login().Handle(PasswordLogin("+970591111111", "Wr0ngPassword"), default));

        unknown.Should().Be(wrong);
        unknown.Code.Should().Be(ErrorCodes.AuthInvalidCredentials);
        unknown.Type.Should().Be(ErrorType.Unauthorized);
        _hasher.SimulateCalls.Should().Be(1, "timing must not reveal that the user does not exist");
        _log.Entries.Select(e => e.Decision).Should().OnlyContain(d => d == "Deny");
    }

    [Fact]
    [Trait("Story", "US-3.1.5-01")]
    [Trait("AC", "AC-03")]
    public async Task Login_FifthFailure_LocksTheAccount_AndTheLockedAccountCannotSignInWithTheRightPassword()
    {
        var account = ActiveUser();
        for (var i = 0; i < 4; i++)
        {
            AppKit.ErrorOf(await Login().Handle(PasswordLogin("+970591111111", "bad"), default)).Code.Should().Be(ErrorCodes.AuthInvalidCredentials);
        }

        var fifth = AppKit.ErrorOf(await Login().Handle(PasswordLogin("+970591111111", "bad"), default));
        var locked = AppKit.ErrorOf(await Login().Handle(PasswordLogin("+970591111111"), default));

        fifth.Code.Should().Be(ErrorCodes.AuthRateLimited);
        fifth.Type.Should().Be(ErrorType.TooManyRequests);
        fifth.RetryAfter.Should().Be(TimeSpan.FromMinutes(15));
        locked.RuleCode.Should().Be(AccountRuleCodes.Locked);
        account.IsLocked(_clock.GetUtcNow().UtcDateTime).Should().BeTrue();
        _sessions.Sessions.Should().BeEmpty();
    }

    [Theory]
    [InlineData("pending", ErrorCodes.AuthAccountPending)]
    [InlineData("banned", ErrorCodes.AdminStateBanned)]
    public async Task Login_NonActiveAccount_WithTheCorrectPassword_IsForbidden(string state, string code)
    {
        var account = AppKit.Pending(_clock);
        _accounts.Add(account);
        if (state == "banned")
        {
            account.Ban(AppKit.Admin(), "x", _clock);
        }

        var error = AppKit.ErrorOf(await Login().Handle(PasswordLogin("+970591111111"), default));

        error.Type.Should().Be(ErrorType.Forbidden);
        error.Code.Should().Be(code);
    }

    [Fact]
    public async Task Login_WhenSeveralActorTypesShareAMobile_TheMatchingPasswordSelectsTheAccount()
    {
        var seeker = AppKit.Active(_clock);
        var employer = AppKit.Pending(_clock, ActorType.Employer);
        _accounts.Add(seeker);
        _accounts.Add(employer);

        var result = await Login().Handle(PasswordLogin("+970591111111"), default);

        result.IsSuccess.Should().BeTrue();
        _sessions.Sessions.Values.Single().AccountId.Should().Be(seeker.Id.Value);
        (await Login().Handle(new AuthenticateUserCommand("+970591111111", AppKit.Password, LoginMechanisms.Password, ActorType.Employer, null, null), default))
            .Error!.Code.Should().Be(ErrorCodes.AuthAccountPending);
    }

    [Fact]
    [Trait("Story", "US-3.1.5-01")]
    [Trait("AC", "AC-02")]
    public async Task Login_Administrator_GetsAnMfaChallengeInsteadOfTokens()
    {
        var admin = Administrator();

        var enrol = await Login().Handle(PasswordLogin("admin@x.com"), default);

        enrol.Value.Status.Should().Be(AuthenticationStatus.MfaEnrollmentRequired);
        enrol.Value.Tokens.Should().BeNull();
        enrol.Value.MfaToken.Should().Be("mfa-0");
        _challenges.Tokens["mfa-0"].Should().Be(admin.Id.Value);
        _sessions.Sessions.Should().BeEmpty();

        admin.BeginMfaEnrollment("protected:SEED");
        admin.ConfirmMfaEnrollment(_clock);
        (await Login().Handle(PasswordLogin("admin@x.com"), default)).Value.Status.Should().Be(AuthenticationStatus.MfaRequired);
    }

    [Fact]
    [Trait("Story", "US-3.1.5-01")]
    [Trait("AC", "AC-02")]
    public async Task Login_MfaMechanism_WithAValidTotp_IssuesTokensMarkedMfaVerified_AndAWrongOneCountsAsAFailure()
    {
        var admin = Administrator();
        admin.BeginMfaEnrollment("protected:SEED");
        admin.ConfirmMfaEnrollment(_clock);

        var wrong = await Login().Handle(new AuthenticateUserCommand("admin@x.com", AppKit.Password, LoginMechanisms.Mfa, null, "000000", null), default);
        var ok = await Login().Handle(new AuthenticateUserCommand("admin@x.com", AppKit.Password, LoginMechanisms.Mfa, null, "654321", null), default);

        AppKit.ErrorOf(wrong).Code.Should().Be(ErrorCodes.AuthInvalidCredentials);
        ok.Value.Status.Should().Be(AuthenticationStatus.Authenticated);
        _tokens.Received().IssueUserToken(Arg.Is<UserTokenRequest>(r => r.MfaVerified && r.ActorType == ActorType.Administrator));
    }

    [Fact]
    [Trait("Story", "US-3.1.5-01")]
    [Trait("AC", "AC-02")]
    public async Task Login_EmailVerification_SendsACodeThenCompletesWithIt()
    {
        var account = ActiveUser();
        account.IssueEmailVerification("h:t", _clock);
        account.VerifyEmail("t", _hasher, _clock);

        var requested = await Login().Handle(new AuthenticateUserCommand("u@example.com", null, LoginMechanisms.EmailVerification, null, null, null), default);
        var wrong = await Login().Handle(new AuthenticateUserCommand("u@example.com", null, LoginMechanisms.EmailVerification, null, null, "000000"), default);
        var completed = await Login().Handle(new AuthenticateUserCommand("u@example.com", null, LoginMechanisms.EmailVerification, null, null, "111222"), default);

        requested.Value.Status.Should().Be(AuthenticationStatus.EmailCodeSent);
        await _email.Received(1).SendLoginCodeAsync(Arg.Is<JobPlatform.SharedKernel.Common.ValueObjects.Email>(e => e.Value == "u@example.com"), "111222", Language.En, Arg.Any<CancellationToken>());
        AppKit.ErrorOf(wrong).Code.Should().Be(ErrorCodes.AuthInvalidCredentials);
        completed.Value.Status.Should().Be(AuthenticationStatus.Authenticated);
    }

    [Fact]
    public async Task Login_EmailVerification_ForUnknownOrUnverifiedEmail_LooksIdenticalAndSendsNothing()
    {
        ActiveUser();

        var unknown = await Login().Handle(new AuthenticateUserCommand("nobody@example.com", null, LoginMechanisms.EmailVerification, null, null, null), default);
        var unverified = await Login().Handle(new AuthenticateUserCommand("u@example.com", null, LoginMechanisms.EmailVerification, null, null, null), default);
        var codeForUnknown = await Login().Handle(new AuthenticateUserCommand("nobody@example.com", null, LoginMechanisms.EmailVerification, null, null, "111222"), default);

        unknown.Value.Status.Should().Be(AuthenticationStatus.EmailCodeSent);
        unverified.Value.Status.Should().Be(AuthenticationStatus.EmailCodeSent);
        AppKit.ErrorOf(codeForUnknown).Code.Should().Be(ErrorCodes.AuthInvalidCredentials);
        await _email.DidNotReceiveWithAnyArgs().SendLoginCodeAsync(default!, default!, default, default);
    }

    // ------------------------------------------------------------------ MFA enrolment and verification

    [Fact]
    public async Task MfaEnrollment_ThenVerification_CompletesTheAdministratorSignIn()
    {
        var admin = Administrator();
        var challenge = (await Login().Handle(PasswordLogin("admin@x.com"), default)).Value.MfaToken!;

        var enrol = await new BeginMfaEnrollmentHandler(_challenges, _accounts, _mfa).Handle(new BeginMfaEnrollmentCommand(challenge), default);
        var verify = new VerifyMfaHandler(_challenges, _accounts, _mfa, Issuer(), _log, AppKit.User(), _clock);
        var wrong = await verify.Handle(new VerifyMfaCommand(challenge, "000000"), default);
        var ok = await verify.Handle(new VerifyMfaCommand(challenge, "654321"), default);

        enrol.Value.Secret.Should().Be("SEED");
        enrol.Value.ProvisioningUri.Should().StartWith("otpauth://");
        admin.MfaSecret.Should().Be("protected:SEED", "only the encrypted seed is stored");
        AppKit.ErrorOf(wrong).Code.Should().Be(ErrorCodes.AuthInvalidCredentials);
        ok.Value.Status.Should().Be(AuthenticationStatus.Authenticated);
        admin.MfaEnabled.Should().BeTrue();
        _challenges.Tokens.Should().BeEmpty("the challenge is single use");
    }

    [Fact]
    public async Task MfaHandlers_RejectUnknownChallenges_AndVerificationWithoutEnrolment()
    {
        var admin = Administrator();
        var verify = new VerifyMfaHandler(_challenges, _accounts, _mfa, Issuer(), _log, AppKit.User(), _clock);
        var enrol = new BeginMfaEnrollmentHandler(_challenges, _accounts, _mfa);

        AppKit.ErrorOf(await enrol.Handle(new BeginMfaEnrollmentCommand("nope"), default)).Code.Should().Be(ErrorCodes.AuthInvalidCredentials);
        AppKit.ErrorOf(await verify.Handle(new VerifyMfaCommand("nope", "123456"), default)).Code.Should().Be(ErrorCodes.AuthInvalidCredentials);

        var challenge = await _challenges.CreateAsync(admin.Id.Value);
        var notStarted = AppKit.ErrorOf(await verify.Handle(new VerifyMfaCommand(challenge.Token, "654321"), default));
        notStarted.RuleCode.Should().Be(AccountRuleCodes.MfaNotStarted);
        notStarted.Type.Should().Be(ErrorType.Conflict);
    }

    // ------------------------------------------------------------------ refresh / logout

    private async Task<(Account Account, string Refresh)> SignedInAsync()
    {
        var account = ActiveUser();
        var result = await Login().Handle(PasswordLogin("+970591111111"), default);
        return (account, result.Value.Tokens!.RefreshToken);
    }

    private RefreshSessionHandler Refresh() => new(_sessions, _accounts, _hasher, new FakeGenerator(), Issuer(), _clock);

    [Fact]
    [Trait("Story", "US-3.1.5-04")]
    [Trait("AC", "AC-02")]
    public async Task Refresh_RotatesTheRefreshTokenAndSlidesTheIdleTimer()
    {
        var (_, refresh) = await SignedInAsync();
        _clock.Advance(TimeSpan.FromMinutes(20));

        var result = await Refresh().Handle(new RefreshSessionCommand(refresh), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Tokens!.RefreshToken.Should().NotBe(refresh);
        _sessions.Sessions.Values.Single().LastActivityUtc.Should().Be(_clock.GetUtcNow().UtcDateTime);
    }

    [Fact]
    public async Task Refresh_ReplayOfARotatedToken_InvalidatesTheSession()
    {
        var (_, refresh) = await SignedInAsync();
        var first = await Refresh().Handle(new RefreshSessionCommand(refresh), default);

        var replay = await Refresh().Handle(new RefreshSessionCommand(refresh), default);
        var afterTheft = await Refresh().Handle(new RefreshSessionCommand(first.Value.Tokens!.RefreshToken), default);

        AppKit.ErrorOf(replay).Code.Should().Be(ErrorCodes.AuthSessionExpired);
        AppKit.ErrorOf(afterTheft).Code.Should().Be(ErrorCodes.AuthSessionExpired);
    }

    [Fact]
    [Trait("Story", "US-3.1.5-04")]
    [Trait("AC", "AC-01")]
    public async Task Refresh_AfterTheIdleTimeout_IsRefused()
    {
        var (_, refresh) = await SignedInAsync();
        _clock.Advance(TimeSpan.FromMinutes(30));

        var error = AppKit.ErrorOf(await Refresh().Handle(new RefreshSessionCommand(refresh), default));

        error.Code.Should().Be(ErrorCodes.AuthSessionExpired);
        error.Type.Should().Be(ErrorType.Unauthorized);
    }

    [Theory]
    [InlineData("garbage")]
    [InlineData("nothex.secret")]
    [InlineData("00000000000000000000000000000000.secret")]
    public async Task Refresh_MalformedOrUnknown_IsRefused(string token) =>
        AppKit.ErrorOf(await Refresh().Handle(new RefreshSessionCommand(token), default)).Code.Should().Be(ErrorCodes.AuthSessionExpired);

    [Fact]
    [Trait("Story", "US-3.1.4-03")]
    [Trait("AC", "AC-01")]
    public async Task Refresh_ForABannedAccount_IsRefusedAndEndsTheSession()
    {
        var (account, refresh) = await SignedInAsync();
        account.Ban(AppKit.Admin(), "x", _clock);

        var error = AppKit.ErrorOf(await Refresh().Handle(new RefreshSessionCommand(refresh), default));

        error.Code.Should().Be(ErrorCodes.AuthSessionExpired);
        _sessions.Sessions.Values.Single().Status.Should().Be(JobPlatform.AccountIdentity.Domain.Sessions.SessionStatus.Invalidated);
    }

    [Fact]
    [Trait("Story", "US-3.1.5-04")]
    [Trait("AC", "AC-04")]
    public async Task Logout_InvalidatesTheSessionAndRevokesTheAccessTokenImmediately()
    {
        var (account, _) = await SignedInAsync();
        var sessionId = _sessions.Sessions.Keys.Single();
        var user = AppKit.User(account.Id.Value, ActorType.JobSeeker, sessionId: sessionId, tokenId: "jti-77");
        var handler = new LogoutHandler(user, _sessions, _tokens, _log, _clock);

        var result = await handler.Handle(new LogoutCommand(), default);

        result.IsSuccess.Should().BeTrue();
        _sessions.Sessions[sessionId].Status.Should().Be(JobPlatform.AccountIdentity.Domain.Sessions.SessionStatus.Invalidated);
        _sessions.RevokedTokens.Should().Contain("jti-77");
        _log.Entries.Should().Contain(e => e.Action == "auth.logout");
    }

    // ------------------------------------------------------------------ change password / verify e-mail

    private ChangePasswordHandler ChangePassword(ICurrentUser user, JobPlatform.AccountIdentity.Domain.PasswordPolicies.PasswordPolicy? policy = null) =>
        new(_accounts, AppKit.PolicyRepo(_clock, policy), _hasher, _sessions, user, _clock);

    [Fact]
    [Trait("Story", "US-3.1.5-02")]
    [Trait("AC", "AC-01")]
    public async Task ChangePassword_WithACompliantPassword_StoresTheNewHashAndEndsOtherSessions()
    {
        var account = ActiveUser();
        var user = AppKit.User(account.Id.Value, ActorType.JobSeeker, sessionId: Guid.NewGuid());

        var result = await ChangePassword(user).Handle(new ChangePasswordCommand(AppKit.Password, "NewPassw0rd"), default);

        result.IsSuccess.Should().BeTrue();
        account.PasswordHash.Value.Should().Be("h:NewPassw0rd");
        _sessions.InvalidatedAccounts.Should().Contain(account.Id.Value);
    }

    [Fact]
    [Trait("Story", "US-3.1.5-02")]
    [Trait("AC", "AC-02")]
    public async Task ChangePassword_WithAWeakPassword_FailsWithInvalidFieldAndViolations()
    {
        var account = ActiveUser();

        var error = AppKit.ErrorOf(await ChangePassword(AppKit.User(account.Id.Value, ActorType.JobSeeker)).Handle(new ChangePasswordCommand(AppKit.Password, "weak"), default));

        error.Code.Should().Be(ErrorCodes.AuthInvalidField);
        error.Type.Should().Be(ErrorType.Validation);
        error.ValidationErrors!["password"].Should().Contain("VAL.Password.MinLength");
        account.PasswordHash.Value.Should().Be("h:" + AppKit.Password);
    }

    [Fact]
    public async Task ChangePassword_WrongCurrentPassword_CountsAsAFailedAttemptTowardTheLockout()
    {
        var account = ActiveUser();
        var handler = ChangePassword(AppKit.User(account.Id.Value, ActorType.JobSeeker));
        for (var i = 0; i < 4; i++)
        {
            AppKit.ErrorOf(await handler.Handle(new ChangePasswordCommand("bad", "NewPassw0rd"), default)).Code.Should().Be(ErrorCodes.AuthInvalidCredentials);
        }

        var fifth = AppKit.ErrorOf(await handler.Handle(new ChangePasswordCommand("bad", "NewPassw0rd"), default));

        fifth.Code.Should().Be(ErrorCodes.AuthRateLimited);
        new ChangePasswordCommand("a", "b").Should().BeAssignableTo<JobPlatform.SharedKernel.Application.Abstractions.IPersistOnFailure>();
    }

    [Fact]
    public async Task ChangePassword_UnknownUser_ReturnsNotFound() =>
        AppKit.ErrorOf(await ChangePassword(AppKit.User(Guid.NewGuid(), ActorType.JobSeeker)).Handle(new ChangePasswordCommand("a", "b"), default)).Type.Should().Be(ErrorType.NotFound);

    [Fact]
    public async Task VerifyEmail_ValidToken_VerifiesTheAddress_AndUnknownAccountIsNotFound()
    {
        var account = ActiveUser();
        account.IssueEmailVerification("h:tok", _clock);
        var handler = new VerifyEmailHandler(_accounts, _hasher, _clock);

        (await handler.Handle(new VerifyEmailCommand(account.Id.Value, "tok"), default)).IsSuccess.Should().BeTrue();

        account.EmailVerifiedAtUtc.Should().NotBeNull();
        AppKit.ErrorOf(await handler.Handle(new VerifyEmailCommand(Guid.NewGuid(), "tok"), default)).Type.Should().Be(ErrorType.NotFound);
        new VerifyEmailCommand(Guid.NewGuid(), "t").RateLimitedErrorCode.Should().Be(ErrorCodes.AuthRateLimited);
    }
}
