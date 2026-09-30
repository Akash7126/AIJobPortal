using JobPlatform.AccountIdentity.Domain.Accounts;
using JobPlatform.AccountIdentity.Domain.Common;
using JobPlatform.AccountIdentity.Domain.Interfaces.Services;
using JobPlatform.AccountIdentity.Domain.Rbac;
using JobPlatform.SharedKernel.Common.Enums;
using JobPlatform.SharedKernel.Common.ValueObjects;
using JobPlatform.SharedKernel.Domain;
using NSubstitute;

namespace JobPlatform.AccountIdentity.Domain.UnitTests;

public class AccountRegistrationTests
{
    [Fact]
    [Trait("Story", "US-3.1.1-01")]
    [Trait("AC", "AC-01")]
    public void Account_Register_JobSeeker_CreatesPendingAccount()
    {
        var clock = TestKit.Clock();

        var account = Account.Register(TestKit.Details(), new PasswordHash(TestKit.PlainVerifier.Hash("x")), clock);

        account.Standing.Should().Be(AccountStanding.Pending);
        account.ActorType.Should().Be(ActorType.JobSeeker);
        account.CreatedAtUtc.Should().Be(TestKit.Start);
        account.ActivatedAtUtc.Should().BeNull();
        account.RoleAssignments.Should().ContainSingle(r => r.RoleId == WellKnownRoles.JobSeeker);
        account.StatusHistory.Should().ContainSingle(h => h.To == AccountStanding.Pending && h.From == null);
    }

    [Fact]
    [Trait("Story", "US-3.1.1-01")]
    [Trait("AC", "AC-04")]
    public void Account_Register_RaisesAccountRegisteredWithActorType()
    {
        var account = Account.Register(TestKit.Details(), new PasswordHash("h"), TestKit.Clock());

        var raised = account.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<AccountRegisteredDomainEvent>().Which;
        raised.AccountId.Should().Be(account.Id);
        raised.ActorType.Should().Be(ActorType.JobSeeker);
        account.Version.Should().Be(1);
    }

    [Fact]
    [Trait("Story", "US-3.1.2-01")]
    [Trait("AC", "AC-01")]
    public void Account_Register_Employer_StoresLevelOneIdentity()
    {
        var details = TestKit.Details(ActorType.Employer, identity: " company-77 ") with { RegistrationNumber = "REG-9", RegistrationLevel = RegistrationLevel.Level1 };

        var account = Account.Register(details, new PasswordHash("h"), TestKit.Clock());

        account.Standing.Should().Be(AccountStanding.Pending);
        account.IdentityKey!.Value.Should().Be("COMPANY-77");
        account.RegistrationNumber.Should().Be("REG-9");
        account.RegistrationLevel.Should().Be(RegistrationLevel.Level1);
        account.RoleAssignments.Should().ContainSingle(r => r.RoleId == WellKnownRoles.Employer);
    }

    [Theory]
    [InlineData(RegistrationLevel.Level1)]
    [InlineData(RegistrationLevel.Level2)]
    [Trait("Story", "US-3.1.2-01")]
    [Trait("AC", "AC-02")]
    public void Account_Register_Employer_AcceptsBothRegistrationLevels(RegistrationLevel level)
    {
        var details = TestKit.Details(ActorType.Employer, identity: "C-1") with { RegistrationLevel = level };

        Account.Register(details, new PasswordHash("h"), TestKit.Clock()).RegistrationLevel.Should().Be(level);
    }

    [Fact]
    [Trait("Story", "US-3.1.2-01")]
    [Trait("AC", "AC-05")]
    public void Account_Register_Employer_RaisesAccountRegistered()
    {
        var account = Account.Register(TestKit.Details(ActorType.Employer, identity: "C-1"), new PasswordHash("h"), TestKit.Clock());

        account.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<AccountRegisteredDomainEvent>()
            .Which.ActorType.Should().Be(ActorType.Employer);
    }

    [Fact]
    [Trait("Story", "US-3.1.3-01")]
    [Trait("AC", "AC-01")]
    public void Account_Register_Partner_CreatesPendingPartnerAccount()
    {
        var account = Account.Register(TestKit.Details(ActorType.ExternalJobSite, identity: "partner.example.com"), new PasswordHash("h"), TestKit.Clock());

        account.Standing.Should().Be(AccountStanding.Pending);
        account.ActorType.Should().Be(ActorType.ExternalJobSite);
    }

    [Fact]
    [Trait("Story", "US-3.1.3-01")]
    [Trait("AC", "AC-03")]
    public void Account_Register_Partner_RaisesAccountRegistered()
    {
        var account = Account.Register(TestKit.Details(ActorType.ExternalJobSite, identity: "p"), new PasswordHash("h"), TestKit.Clock());

        account.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<AccountRegisteredDomainEvent>()
            .Which.ActorType.Should().Be(ActorType.ExternalJobSite);
    }

    [Theory]
    [InlineData(ActorType.Employer)]
    [InlineData(ActorType.ExternalJobSite)]
    public void Account_Register_WithoutIdentityKey_ThrowsIdentityKeyRequired(ActorType type)
    {
        var act = () => Account.Register(TestKit.Details(type), new PasswordHash("h"), TestKit.Clock());

        act.ShouldBreakRule(AccountRuleCodes.IdentityKeyRequired, ErrorCodes.AuthInvalidField, BusinessRuleKind.InvalidInput);
    }

    [Theory]
    [InlineData(ActorType.Administrator)]
    [InlineData(ActorType.Guest)]
    [InlineData(ActorType.System)]
    public void Account_Register_NonSelfRegistrableActor_ThrowsActorTypeNotRegistrable(ActorType type)
    {
        var act = () => Account.Register(TestKit.Details(type, identity: "x"), new PasswordHash("h"), TestKit.Clock());

        act.ShouldBreakRule(AccountRuleCodes.ActorTypeNotRegistrable);
    }

    [Fact]
    public void Account_CreateAdministrator_IsActiveAndRequiresMfa()
    {
        var admin = TestKit.AdministratorAccount();

        admin.Standing.Should().Be(AccountStanding.Active);
        admin.MfaRequired.Should().BeTrue();
        admin.RoleAssignments.Should().ContainSingle(r => r.RoleId == WellKnownRoles.Administrator);
        admin.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void Account_PasswordHash_NeverPrintsTheHash()
    {
        new PasswordHash("secret-hash").ToString().Should().NotContain("secret");
        var create = () => new PasswordHash(" ");
        create.Should().Throw<ArgumentException>();
    }

    // -------------------------------------------------------------- AccountRegistrar (INV-01)

    [Theory]
    [InlineData(ActorType.JobSeeker, ErrorCodes.JobSeekerDuplicate)]
    [InlineData(ActorType.Employer, ErrorCodes.EmployerDuplicate)]
    [InlineData(ActorType.ExternalJobSite, ErrorCodes.PartnerDuplicate)]
    [Trait("Story", "US-3.1.1-01")]
    [Trait("AC", "AC-02")]
    public async Task AccountRegistrar_DuplicateMobile_ThrowsActorSpecificDuplicateCode(ActorType type, string external)
    {
        var checker = Substitute.For<IAccountUniquenessChecker>();
        checker.IsMobileTakenAsync(type, Arg.Any<MobileNumber>(), Arg.Any<CancellationToken>()).Returns(true);
        var registrar = new AccountRegistrar(checker, TestKit.Clock());

        var act = () => registrar.RegisterAsync(TestKit.Details(type, identity: type == ActorType.JobSeeker ? null : "X"), new PasswordHash("h"));

        var ex = (await act.Should().ThrowAsync<BusinessRuleViolationException>()).Which;
        ex.Code.Should().Be(AccountRuleCodes.Duplicate);
        ex.ExternalCode.Should().Be(external);
        ex.Kind.Should().Be(BusinessRuleKind.Conflict);
    }

    [Fact]
    [Trait("Story", "US-3.1.1-01")]
    [Trait("AC", "AC-02")]
    public async Task AccountRegistrar_DuplicateEmail_Throws()
    {
        var checker = Substitute.For<IAccountUniquenessChecker>();
        checker.IsEmailTakenAsync(ActorType.JobSeeker, Arg.Any<Email>(), Arg.Any<CancellationToken>()).Returns(true);

        var act = () => new AccountRegistrar(checker, TestKit.Clock()).RegisterAsync(TestKit.Details(), new PasswordHash("h"));

        (await act.Should().ThrowAsync<BusinessRuleViolationException>()).Which.ExternalCode.Should().Be(ErrorCodes.JobSeekerDuplicate);
    }

    [Fact]
    [Trait("Story", "US-3.1.2-01")]
    [Trait("AC", "AC-03")]
    public async Task AccountRegistrar_DuplicateCompanyId_ThrowsEmployerDuplicate()
    {
        var checker = Substitute.For<IAccountUniquenessChecker>();
        checker.IsIdentityKeyTakenAsync(ActorType.Employer, Arg.Any<ExternalIdentityKey>(), Arg.Any<CancellationToken>()).Returns(true);

        var act = () => new AccountRegistrar(checker, TestKit.Clock()).RegisterAsync(TestKit.Details(ActorType.Employer, identity: "C-1"), new PasswordHash("h"));

        (await act.Should().ThrowAsync<BusinessRuleViolationException>()).Which.ExternalCode.Should().Be(ErrorCodes.EmployerDuplicate);
    }

    [Fact]
    [Trait("Story", "US-3.1.3-01")]
    [Trait("AC", "AC-02")]
    public async Task AccountRegistrar_DuplicatePartnerIdentity_ThrowsPartnerDuplicate()
    {
        var checker = Substitute.For<IAccountUniquenessChecker>();
        checker.IsIdentityKeyTakenAsync(ActorType.ExternalJobSite, Arg.Any<ExternalIdentityKey>(), Arg.Any<CancellationToken>()).Returns(true);

        var act = () => new AccountRegistrar(checker, TestKit.Clock()).RegisterAsync(TestKit.Details(ActorType.ExternalJobSite, identity: "p"), new PasswordHash("h"));

        (await act.Should().ThrowAsync<BusinessRuleViolationException>()).Which.ExternalCode.Should().Be(ErrorCodes.PartnerDuplicate);
    }

    [Fact]
    public async Task AccountRegistrar_UniqueDetails_RegistersAccount()
    {
        var checker = Substitute.For<IAccountUniquenessChecker>();

        var account = await new AccountRegistrar(checker, TestKit.Clock()).RegisterAsync(TestKit.Details(), new PasswordHash("h"));

        account.Standing.Should().Be(AccountStanding.Pending);
    }
}

public class ValueObjectTests
{
    [Theory]
    [InlineData("+970591111111", true)]
    [InlineData("+970 59-111 1111", true)]
    [InlineData("00970591111111", false)]
    [InlineData("0591111111", false)]
    [InlineData("+12", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void MobileNumber_TryCreate_ValidatesE164(string? input, bool valid) =>
        MobileNumber.TryCreate(input, out _).Should().Be(valid);

    [Fact]
    public void MobileNumber_NormalisesAndMasks()
    {
        var number = MobileNumber.Create("+970 59-111 1111");

        number.Value.Should().Be("+970591111111");
        number.Masked.Should().Be("+970****1111").And.NotContain("59111");
        MobileNumber.Create("+970591111111").Should().Be(number);
        number.ToString().Should().Be(number.Value);
    }

    [Theory]
    [InlineData("User@Example.COM", "user@example.com")]
    [InlineData("  a@b.co ", "a@b.co")]
    public void Email_Create_NormalisesToLowerCase(string input, string expected) => Email.Create(input).Value.Should().Be(expected);

    [Theory]
    [InlineData("nope")]
    [InlineData("a@b")]
    [InlineData("")]
    public void Email_TryCreate_RejectsMalformed(string input) => Email.TryCreate(input, out _).Should().BeFalse();

    [Fact]
    public void Email_TryCreate_RejectsTooLong() => Email.TryCreate(new string('a', 250) + "@x.com", out _).Should().BeFalse();

    [Fact]
    public void Email_And_Mobile_Create_ThrowOnInvalid()
    {
        var email = () => Email.Create("x");
        var mobile = () => MobileNumber.Create("x");
        email.Should().Throw<ArgumentException>();
        mobile.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void ExternalIdentityKey_NormalisesAndValidates()
    {
        ExternalIdentityKey.Create(" abc-1 ").Value.Should().Be("ABC-1");
        ExternalIdentityKey.TryCreate("", out _).Should().BeFalse();
        ExternalIdentityKey.TryCreate(new string('x', 129), out _).Should().BeFalse();
        var create = () => ExternalIdentityKey.Create(" ");
        create.Should().Throw<ArgumentException>();
        ExternalIdentityKey.Create("a").Equals(ExternalIdentityKey.Create("A")).Should().BeTrue();
        ExternalIdentityKey.Create("a").GetHashCode().Should().Be(ExternalIdentityKey.Create("A").GetHashCode());
    }

    [Fact]
    public void LocalizedText_SelectsByLanguage()
    {
        var text = new LocalizedText("مرحبا", "Hello");

        text.For(Language.Ar).Should().Be("مرحبا");
        text.For(Language.En).Should().Be("Hello");
        text.Should().Be(new LocalizedText("مرحبا", "Hello"));
    }

    [Fact]
    public void BusinessRule_BrokenRule_ThrowsWithCodeAndKind()
    {
        var rule = new BusinessRule("AI.X.Y", "msg", isBroken: true, "E-X", BusinessRuleKind.Conflict);

        var act = () => { if (rule.IsBroken()) { throw new BusinessRuleViolationException(rule); } };

        var ex = act.Should().Throw<BusinessRuleViolationException>().Which;
        ex.Code.Should().Be("AI.X.Y");
        ex.ExternalCode.Should().Be("E-X");
        ex.Kind.Should().Be(BusinessRuleKind.Conflict);
        ex.Args.Should().BeEmpty();
    }
}
