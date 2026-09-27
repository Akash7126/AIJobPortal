using JobPlatform.AccountIdentity.Application.Abstractions;
using JobPlatform.AccountIdentity.Application.Authentication;
using JobPlatform.AccountIdentity.Application.Security;
using JobPlatform.AccountIdentity.Domain.Accounts;
using JobPlatform.AccountIdentity.Domain.Common;
using JobPlatform.SharedKernel.Application.Ports;
using JobPlatform.SharedKernel.Common.ValueObjects;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;

namespace JobPlatform.AccountIdentity.Application.UnitTests;

/// <summary>Rehash-on-login and TOTP replay protection at handler level.</summary>
public class AuthenticationHardeningTests
{
    private readonly FakeTimeProvider _clock = AppKit.Clock();
    private readonly InMemoryAccounts _accounts = new();
    private readonly FakeHasher _hasher = new();
    private readonly InMemorySessionStore _sessions = new();
    private readonly InMemoryMfaChallenges _challenges = new();
    private readonly RecordingAccessLog _log = new();
    private readonly IMfaService _mfa = Substitute.For<IMfaService>();

    public AuthenticationHardeningTests()
    {
        _mfa.Unprotect(Arg.Any<string>()).Returns(ci => ci.Arg<string>().Replace("protected:", string.Empty));
        _mfa.FindMatchingTimeStep(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<DateTime>())
            .Returns(ci => ci.ArgAt<string>(1) == "654321" ? (long?)(ci.ArgAt<DateTime>(2).Ticks / (TimeSpan.TicksPerSecond * 30)) : null);
    }

    private SessionIssuer Issuer() => new(AppKit.TimeoutRepo(_clock), _sessions, AppKit.Tokens(), _hasher, new FakeGenerator(), _clock);

    private AuthenticateUserHandler Login() => new(_accounts, _hasher, _hasher, new InMemoryLoginCodes(), Substitute.For<IEmailVerificationSender>(),
        new FakeGenerator(), _mfa, _challenges, Issuer(), _log, AppKit.User(), _clock, NullLogger<AuthenticateUserHandler>.Instance);

    private Account EnrolledAdministrator()
    {
        var admin = Account.CreateAdministrator("Admin", Email.Create("admin@x.com"), MobileNumber.Create("+970592222222"),
            new PasswordHash("h:" + AppKit.Password), 1, _clock);
        admin.BeginMfaEnrollment("protected:SEED");
        admin.ConfirmMfaEnrollment(_clock);
        _accounts.Add(admin);
        return admin;
    }

    [Fact]
    [Trait("Story", "US-3.1.5-02")]
    [Trait("AC", "AC-04")]
    public async Task Login_WithAWeakerStoredHash_UpgradesItTransparently_AndOnlyAfterASuccessfulVerification()
    {
        var account = AppKit.Active(_clock);
        _accounts.Add(account);
        _hasher.RehashNeeded = true;

        var wrong = await Login().Handle(new AuthenticateUserCommand("+970591111111", "Wr0ngPassword", LoginMechanisms.Password, null, null, null), default);
        var hashAfterFailure = account.PasswordHash.Value;
        var ok = await Login().Handle(new AuthenticateUserCommand("+970591111111", AppKit.Password, LoginMechanisms.Password, null, null, null), default);

        wrong.IsFailure.Should().BeTrue();
        hashAfterFailure.Should().Be("h:" + AppKit.Password, "a failed attempt never rewrites the hash");
        ok.IsSuccess.Should().BeTrue();
        account.PasswordHash.Value.Should().Be("h2:" + AppKit.Password);
    }

    [Fact]
    public async Task Login_WithACurrentHash_LeavesItAlone()
    {
        var account = AppKit.Active(_clock);
        _accounts.Add(account);

        (await Login().Handle(new AuthenticateUserCommand("+970591111111", AppKit.Password, LoginMechanisms.Password, null, null, null), default))
            .IsSuccess.Should().BeTrue();

        account.PasswordHash.Value.Should().Be("h:" + AppKit.Password);
    }

    [Fact]
    [Trait("Story", "US-3.1.5-01")]
    [Trait("AC", "AC-02")]
    public async Task Login_WithMfa_RefusesAReplayedTotpCode_UntilTheNextTimeStep()
    {
        var admin = EnrolledAdministrator();
        var command = new AuthenticateUserCommand("admin@x.com", AppKit.Password, LoginMechanisms.Mfa, null, "654321", null);

        var first = await Login().Handle(command, default);
        var replay = await Login().Handle(command, default);
        _clock.Advance(TimeSpan.FromSeconds(30));
        var nextStep = await Login().Handle(command, default);

        first.IsSuccess.Should().BeTrue();
        AppKit.ErrorOf(replay).Code.Should().Be(ErrorCodes.AuthInvalidCredentials);
        nextStep.IsSuccess.Should().BeTrue();
        admin.MfaLastUsedTimeStep.Should().NotBeNull();
    }
}
