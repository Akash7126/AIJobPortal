using JobPlatform.AccountIdentity.Application.Commands.Accounts;
using JobPlatform.AccountIdentity.Application.DTOs.Accounts;
using JobPlatform.AccountIdentity.Application.Handlers.Accounts;
using JobPlatform.AccountIdentity.Application.Interfaces;
using JobPlatform.AccountIdentity.Application.Services.Accounts;
using JobPlatform.AccountIdentity.Domain.Accounts;
using JobPlatform.AccountIdentity.Domain.Common;
using JobPlatform.AccountIdentity.Domain.Interfaces.Services;
using JobPlatform.AccountIdentity.Domain.PasswordPolicies;
using JobPlatform.SharedKernel.Application.Interfaces.Ports;
using JobPlatform.SharedKernel.Application.Ports;
using JobPlatform.SharedKernel.Application.Results;
using JobPlatform.SharedKernel.Common.Enums;
using JobPlatform.SharedKernel.Common.ValueObjects;
using JobPlatform.SharedKernel.Domain;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace JobPlatform.AccountIdentity.Application.UnitTests;

public class RegistrationHandlerTests
{
    private readonly Microsoft.Extensions.Time.Testing.FakeTimeProvider _clock = AppKit.Clock();
    private readonly InMemoryAccounts _accounts = new();
    private readonly FakeHasher _hasher = new();
    private readonly FakeGenerator _generator = new();
    private readonly IOtpSender _otp = Substitute.For<IOtpSender>();
    private readonly IEmailVerificationSender _email = Substitute.For<IEmailVerificationSender>();
    private readonly IAccountUniquenessChecker _uniqueness = Substitute.For<IAccountUniquenessChecker>();
    private PasswordPolicy _policy = null!;

    private RegistrationWorkflow Workflow()
    {
        _policy = AppKit.Policy(_clock);
        return new RegistrationWorkflow(AppKit.PolicyRepo(_clock, _policy), _hasher, new AccountRegistrar(_uniqueness, _clock), _accounts, _hasher, _hasher,
            _generator, _otp, _email, AppKit.User(), _clock, NullLogger<RegistrationWorkflow>.Instance);
    }

    private static RegisterJobSeekerAccountCommand SeekerCommand(string password = AppKit.Password, string? email = "sara@example.com") =>
        new("Sara", "+970591111111", email, password, null, null);

    [Fact]
    [Trait("Story", "US-3.1.1-01")]
    [Trait("AC", "AC-01")]
    public async Task RegisterJobSeeker_CreatesAPendingAccount_WithAHashedPasswordAndAHashedOtp()
    {
        var handler = new RegisterJobSeekerAccountHandler(Workflow());

        var result = await handler.Handle(SeekerCommand(), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Standing.Should().Be("Pending");
        result.Value.NextStep.Should().Be(RegistrationNextStep.ActivateWithMobileCode);
        result.Value.ActivationCodeExpiresAtUtc.Should().Be(_clock.GetUtcNow().UtcDateTime.AddMinutes(10));
        var account = _accounts.Items.Should().ContainSingle().Which;
        account.PasswordHash.Value.Should().Be("h:" + AppKit.Password);
        account.ActivationChallenge!.CodeHash.Should().Be("h:123456");
        account.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<AccountRegisteredDomainEvent>();
    }

    [Fact]
    [Trait("Story", "US-3.1.1-01")]
    [Trait("AC", "AC-01")]
    public async Task RegisterJobSeeker_SendsTheOtpAndVerificationToken_ButNeverPutsThemInTheResult()
    {
        var handler = new RegisterJobSeekerAccountHandler(Workflow());

        var result = await handler.Handle(SeekerCommand(), default);

        await _otp.Received(1).SendActivationCodeAsync(Arg.Is<MobileNumber>(m => m.Value == "+970591111111"), "123456", Language.En, Arg.Any<CancellationToken>());
        await _email.Received(1).SendVerificationAsync(Arg.Is<Email>(e => e.Value == "sara@example.com"), Arg.Any<Guid>(), Arg.Any<string>(), Language.En, Arg.Any<CancellationToken>());
        System.Text.Json.JsonSerializer.Serialize(result.Value).Should().NotContain("123456").And.NotContain(AppKit.Password);
        _accounts.Items.Single().EmailVerificationTokenHash.Should().StartWith("h:token-");
    }

    [Fact]
    public async Task RegisterJobSeeker_WithoutEmail_SkipsEmailVerification()
    {
        var handler = new RegisterJobSeekerAccountHandler(Workflow());

        await handler.Handle(SeekerCommand(email: null), default);

        await _email.DidNotReceiveWithAnyArgs().SendVerificationAsync(default!, default, default!, default, default);
        _accounts.Items.Single().EmailVerificationTokenHash.Should().BeNull();
    }

    [Fact]
    public async Task RegisterJobSeeker_WhenDeliveryFails_StillRegistersSoTheUserCanRequestANewCode()
    {
        _otp.SendActivationCodeAsync(default!, default!, default, default).ReturnsForAnyArgs(Task.FromException(new InvalidOperationException("gateway down")));
        var handler = new RegisterJobSeekerAccountHandler(Workflow());

        var result = await handler.Handle(SeekerCommand(), default);

        result.IsSuccess.Should().BeTrue();
        _accounts.Items.Should().ContainSingle();
    }

    [Fact]
    [Trait("Story", "US-3.1.5-02")]
    [Trait("AC", "AC-02")]
    public async Task RegisterJobSeeker_WithAWeakPassword_ThrowsInvalidFieldAndCreatesNothing()
    {
        var handler = new RegisterJobSeekerAccountHandler(Workflow());

        var act = () => handler.Handle(SeekerCommand("weak"), default);

        (await act.Should().ThrowAsync<BusinessRuleViolationException>()).Which.ExternalCode.Should().Be(ErrorCodes.AuthInvalidField);
        _accounts.Items.Should().BeEmpty();
    }

    [Fact]
    [Trait("Story", "US-3.1.1-01")]
    [Trait("AC", "AC-02")]
    public async Task RegisterJobSeeker_Duplicate_ThrowsTheDuplicateRule()
    {
        _uniqueness.IsMobileTakenAsync(ActorType.JobSeeker, Arg.Any<MobileNumber>(), Arg.Any<CancellationToken>()).Returns(true);
        var handler = new RegisterJobSeekerAccountHandler(Workflow());

        var act = () => handler.Handle(SeekerCommand(), default);

        (await act.Should().ThrowAsync<BusinessRuleViolationException>()).Which.ExternalCode.Should().Be(ErrorCodes.JobSeekerDuplicate);
    }

    [Theory]
    [InlineData(1, RegistrationLevel.Level1)]
    [InlineData(2, RegistrationLevel.Level2)]
    [Trait("Story", "US-3.1.2-01")]
    [Trait("AC", "AC-01")]
    public async Task RegisterEmployer_StoresLevelOneIdentityAndTheRequestedLevel(int level, RegistrationLevel expected)
    {
        var handler = new RegisterEmployerAccountHandler(Workflow());

        var result = await handler.Handle(new RegisterEmployerAccountCommand("Acme", "hr@acme.com", "+970592222222", "c-99", "REG-1", AppKit.Password, level, null), default);

        result.Value.ActorType.Should().Be("Employer");
        var account = _accounts.Items.Single();
        account.IdentityKey!.Value.Should().Be("C-99");
        account.RegistrationNumber.Should().Be("REG-1");
        account.RegistrationLevel.Should().Be(expected);
        await _otp.Received(1).SendActivationCodeAsync(Arg.Any<MobileNumber>(), "123456", Arg.Any<Language>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    [Trait("Story", "US-3.1.3-01")]
    [Trait("AC", "AC-01")]
    public async Task RegisterPartner_CreatesAPendingAccount_WithoutAnyMobileCode()
    {
        var handler = new RegisterPartnerAccountHandler(Workflow());

        var result = await handler.Handle(new RegisterPartnerAccountCommand("Jobs Ltd", "it@jobs.com", "+970593333333", "jobs.example.com", AppKit.Password, null), default);

        result.Value.NextStep.Should().Be(RegistrationNextStep.AwaitStaffApproval);
        result.Value.ActivationCodeExpiresAtUtc.Should().BeNull();
        _accounts.Items.Single().ActivationChallenge.Should().BeNull();
        await _otp.DidNotReceiveWithAnyArgs().SendActivationCodeAsync(default!, default!, default, default);
    }

    [Fact]
    public void RegistrationCommands_DeclareTheirRateLimitScopeAndCodes()
    {
        SeekerCommand().RateLimitedErrorCode.Should().Be(ErrorCodes.JobSeekerRateLimited);
        SeekerCommand().UniqueViolationErrorCode.Should().Be(ErrorCodes.JobSeekerDuplicate);
        ((JobPlatform.SharedKernel.Application.Interfaces.Cqrs.IRateLimitedRequest)SeekerCommand()).RateLimitPermits.Should().Be(5);
        ((JobPlatform.SharedKernel.Application.Interfaces.Cqrs.IRateLimitedRequest)SeekerCommand()).RateLimitWindow.Should().Be(TimeSpan.FromMinutes(15));
        new RegisterEmployerAccountCommand("a", "b", "c", "d", "e", "f", 1, null).RateLimitedErrorCode.Should().Be(ErrorCodes.EmployerRateLimited);
        new RegisterPartnerAccountCommand("a", "b", "c", "d", "e", null).UniqueViolationErrorCode.Should().Be(ErrorCodes.PartnerDuplicate);
        new RegisterEmployerAccountCommand("a", "b", "c", "d", "e", "f", 1, null).RateLimitScope.Should().NotBe(SeekerCommand().RateLimitScope);
    }
}

public class ActivationHandlerTests
{
    private readonly Microsoft.Extensions.Time.Testing.FakeTimeProvider _clock = AppKit.Clock();
    private readonly InMemoryAccounts _accounts = new();
    private readonly IRateLimiter _limiter = Substitute.For<IRateLimiter>();
    private readonly FakeHasher _hasher = new();
    private readonly RecordingAccessLog _log = new();

    public ActivationHandlerTests() =>
        _limiter.HitAsync(default!, default, default, default).ReturnsForAnyArgs(new RateLimitDecision(true, 1, TimeSpan.Zero));

    private ActivateAccountHandler Activate() => new(_accounts, _limiter, AppKit.User(), _hasher, _log, _clock);

    private Account PendingWithCode(ActorType type = ActorType.JobSeeker)
    {
        var account = AppKit.Pending(_clock, type);
        account.IssueActivationChallenge("h:123456", _clock);
        _accounts.Add(account);
        return account;
    }

    [Fact]
    [Trait("Story", "US-3.1.1-02")]
    [Trait("AC", "AC-01")]
    public async Task Activate_WithTheCorrectCode_ActivatesAndLogsAnAllow()
    {
        var account = PendingWithCode();

        var result = await Activate().Handle(new ActivateAccountCommand(account.Id.Value, "123456"), default);

        result.IsSuccess.Should().BeTrue();
        account.Standing.Should().Be(AccountStanding.Active);
        _log.Entries.Should().ContainSingle(e => e.Action == "account.activate" && e.Decision == "Allow" && e.AccountId == account.Id.Value);
    }

    [Fact]
    [Trait("Story", "US-3.1.1-02")]
    [Trait("AC", "AC-02")]
    public async Task Activate_WithAWrongCode_FailsWithExpired_ButTheAttemptIsRecordedOnTheAggregate()
    {
        var account = PendingWithCode();

        var result = await Activate().Handle(new ActivateAccountCommand(account.Id.Value, "000000"), default);

        var error = AppKit.ErrorOf(result);
        error.Code.Should().Be(ErrorCodes.JobSeekerExpired);
        error.Type.Should().Be(ErrorType.BusinessRule);
        error.RuleCode.Should().Be(AccountRuleCodes.CodeInvalidOrExpired);
        account.ActivationChallenge!.Attempts.Should().Be(1, "the command is IPersistOnFailure");
        _log.Entries.Should().ContainSingle(e => e.Decision == "Deny" && e.Reason == AccountRuleCodes.CodeInvalidOrExpired);
        new ActivateAccountCommand(Guid.NewGuid(), "1").Should().BeAssignableTo<JobPlatform.SharedKernel.Application.Interfaces.Cqrs.IPersistOnFailure>();
    }

    [Theory]
    [InlineData(ActorType.JobSeeker, ErrorCodes.JobSeekerRateLimited)]
    [InlineData(ActorType.Employer, ErrorCodes.EmployerRateLimited)]
    [Trait("Story", "US-3.1.1-02")]
    [Trait("AC", "AC-03")]
    public async Task Activate_WhenTheSourceLimiterRefuses_Returns429WithTheActorSpecificCode(ActorType type, string code)
    {
        var account = PendingWithCode(type);
        _limiter.HitAsync(default!, default, default, default).ReturnsForAnyArgs(new RateLimitDecision(false, 6, TimeSpan.FromMinutes(9)));

        var error = AppKit.ErrorOf(await Activate().Handle(new ActivateAccountCommand(account.Id.Value, "123456"), default));

        error.Code.Should().Be(code);
        error.Type.Should().Be(ErrorType.TooManyRequests);
        error.RetryAfter.Should().Be(TimeSpan.FromMinutes(9));
        account.Standing.Should().Be(AccountStanding.Pending, "the aggregate is not touched when the limiter refuses");
        await _limiter.Received().HitAsync(Arg.Is<string>(k => k.StartsWith("activation:src-1:")), 5, TimeSpan.FromMinutes(15), Arg.Any<CancellationToken>());
    }

    [Fact]
    [Trait("Story", "US-3.1.1-02")]
    [Trait("AC", "AC-03")]
    public async Task Activate_SixthAttemptOnTheAggregate_ReturnsTooManyRequests()
    {
        var account = PendingWithCode();
        for (var i = 0; i < 5; i++)
        {
            await Activate().Handle(new ActivateAccountCommand(account.Id.Value, "000000"), default);
        }

        var error = AppKit.ErrorOf(await Activate().Handle(new ActivateAccountCommand(account.Id.Value, "123456"), default));

        error.Type.Should().Be(ErrorType.TooManyRequests);
        error.Code.Should().Be(ErrorCodes.JobSeekerRateLimited);
    }

    [Fact]
    public async Task Activate_UnknownAccount_ReturnsNotFound()
    {
        var error = AppKit.ErrorOf(await Activate().Handle(new ActivateAccountCommand(Guid.NewGuid(), "123456"), default));

        error.Type.Should().Be(ErrorType.NotFound);
        error.Code.Should().Be(ErrorCodes.NotFound);
    }

    [Fact]
    public async Task ResendActivationCode_IssuesAndSendsANewCode_AndSwallowsDeliveryFailures()
    {
        var account = PendingWithCode();
        var sender = Substitute.For<IOtpSender>();
        var generator = new FakeGenerator { Otp = "654321" };
        var handler = new ResendActivationCodeHandler(_accounts, _limiter, AppKit.User(), _hasher, generator, sender, _clock, NullLogger<ResendActivationCodeHandler>.Instance);

        var ok = await handler.Handle(new ResendActivationCodeCommand(account.Id.Value), default);

        ok.IsSuccess.Should().BeTrue();
        account.ActivationChallenge!.CodeHash.Should().Be("h:654321");
        await sender.Received(1).SendActivationCodeAsync(account.Mobile, "654321", Language.En, Arg.Any<CancellationToken>());

        sender.SendActivationCodeAsync(default!, default!, default, default).ReturnsForAnyArgs(Task.FromException(new InvalidOperationException("down")));
        (await handler.Handle(new ResendActivationCodeCommand(account.Id.Value), default)).IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task ResendActivationCode_UnknownAccountOrThrottled_Fails()
    {
        var sender = Substitute.For<IOtpSender>();
        var handler = new ResendActivationCodeHandler(_accounts, _limiter, AppKit.User(), _hasher, new FakeGenerator(), sender, _clock, NullLogger<ResendActivationCodeHandler>.Instance);
        AppKit.ErrorOf(await handler.Handle(new ResendActivationCodeCommand(Guid.NewGuid()), default)).Type.Should().Be(ErrorType.NotFound);

        var account = PendingWithCode();
        _limiter.HitAsync(default!, default, default, default).ReturnsForAnyArgs(new RateLimitDecision(false, 6, TimeSpan.FromMinutes(1)));
        AppKit.ErrorOf(await handler.Handle(new ResendActivationCodeCommand(account.Id.Value), default)).Type.Should().Be(ErrorType.TooManyRequests);
        await sender.DidNotReceiveWithAnyArgs().SendActivationCodeAsync(default!, default!, default, default);
    }

    [Fact]
    [Trait("Story", "US-3.1.3-01")]
    [Trait("AC", "AC-04")]
    public async Task ApprovePartner_ActivatesThePartnerAsAuthorisedStaff()
    {
        var partner = AppKit.Pending(_clock, ActorType.ExternalJobSite);
        _accounts.Add(partner);
        var staffId = Guid.NewGuid();
        var handler = new ApprovePartnerAccountHandler(_accounts, AppKit.User(staffId, ActorType.Administrator), _clock);

        var result = await handler.Handle(new ApprovePartnerAccountCommand(partner.Id.Value), default);

        result.IsSuccess.Should().BeTrue();
        partner.Standing.Should().Be(AccountStanding.Active);
        partner.DomainEvents.OfType<AccountActivatedDomainEvent>().Single().ActorId.Should().Be(staffId);
        AppKit.ErrorOf(await handler.Handle(new ApprovePartnerAccountCommand(Guid.NewGuid()), default)).Type.Should().Be(ErrorType.NotFound);
    }

    [Fact]
    [Trait("Story", "US-3.1.3-01")]
    [Trait("AC", "AC-04")]
    public void ApprovePartner_RequiresTheApprovePartnerPermissionAndAdministratorWithMfa()
    {
        var command = new ApprovePartnerAccountCommand(Guid.NewGuid());

        command.RequiredPermission.Should().Be("accounts.approve-partner");
        command.AllowedActorTypes.Should().Equal(ActorType.Administrator);
        command.RequireMfa.Should().BeTrue();
        command.ForbiddenErrorCode.Should().Be(ErrorCodes.AdminForbidden);
    }
}
