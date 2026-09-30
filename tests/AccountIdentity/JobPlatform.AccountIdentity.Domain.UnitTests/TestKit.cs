using JobPlatform.AccountIdentity.Domain.Accounts;
using JobPlatform.AccountIdentity.Domain.Common;
using JobPlatform.AccountIdentity.Domain.Interfaces.Services;
using JobPlatform.AccountIdentity.Domain.PasswordPolicies;
using JobPlatform.SharedKernel.Common.Enums;
using JobPlatform.SharedKernel.Common.ValueObjects;
using JobPlatform.SharedKernel.Domain;
using Microsoft.Extensions.Time.Testing;

namespace JobPlatform.AccountIdentity.Domain.UnitTests;

/// <summary>Deterministic fakes and builders. Aggregates are built through their public behaviour, never by reflection.</summary>
internal static class TestKit
{
    public static readonly DateTime Start = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
    public const string Password = "Str0ngPass";
    public const string Mobile = "+970591111111";

    public static FakeTimeProvider Clock() => new(new DateTimeOffset(Start));

    /// <summary>Hash format "h:&lt;secret&gt;" - lets tests control hashing without crypto.</summary>
    public sealed class PlainVerifier : ISecretVerifier
    {
        public static readonly PlainVerifier Instance = new();

        public static string Hash(string secret) => "h:" + secret;

        public bool Verify(string secret, string hash) => hash == Hash(secret);
    }

    public static Actor Admin() => Actor.Administrator(Guid.NewGuid());

    public static Actor Staff() => Actor.AuthorisedStaff(Guid.NewGuid());

    public static Actor Nobody() => Actor.NonPrivileged(Guid.NewGuid());

    public static PasswordPolicy Policy(TimeProvider? clock = null) => PasswordPolicy.CreateDefault(clock ?? Clock());

    public static RegistrationDetails Details(ActorType type = ActorType.JobSeeker, string mobile = Mobile, string? email = "user@example.com",
        string? identity = null) =>
        new(type, "Test User", email is null ? null : Email.Create(email), MobileNumber.Create(mobile),
            identity is null ? null : ExternalIdentityKey.Create(identity));

    public static Account Pending(ActorType type = ActorType.JobSeeker, TimeProvider? clock = null, string mobile = Mobile, string? email = "user@example.com")
    {
        var identity = type == ActorType.JobSeeker ? null : "COMPANY-1";
        var account = Account.Register(Details(type, mobile, email, identity), new PasswordHash(PlainVerifier.Hash(Password)), clock ?? Clock());
        account.ClearDomainEvents();
        return account;
    }

    /// <summary>A job seeker or employer that went through OTP activation (code "123456").</summary>
    public static Account Active(ActorType type = ActorType.JobSeeker, TimeProvider? clock = null)
    {
        clock ??= Clock();
        var account = Pending(type, clock);
        if (type == ActorType.ExternalJobSite)
        {
            account.ApproveByStaff(Staff(), clock);
        }
        else
        {
            account.IssueActivationChallenge(PlainVerifier.Hash("123456"), clock);
            account.Activate("123456", PlainVerifier.Instance, clock);
        }

        account.ClearDomainEvents();
        return account;
    }

    public static Account AdministratorAccount(TimeProvider? clock = null) =>
        Account.CreateAdministrator("Admin", Email.Create("admin@example.com"), MobileNumber.Create("+970592222222"),
            new PasswordHash(PlainVerifier.Hash(Password)), 1, clock ?? Clock());

    public static Account Banned(TimeProvider? clock = null)
    {
        clock ??= Clock();
        var account = Active(ActorType.JobSeeker, clock);
        account.Ban(Admin(), "abuse", clock);
        account.ClearDomainEvents();
        return account;
    }

    public static void ShouldBreakRule<T>(this Func<T> action, string code, string? externalCode = null, BusinessRuleKind? kind = null) =>
        ((Action)(() => action())).ShouldBreakRule(code, externalCode, kind);

    public static void ShouldBreakRule(this Action action, string code, string? externalCode = null, BusinessRuleKind? kind = null)
    {
        var exception = action.Should().Throw<BusinessRuleViolationException>().Which;
        exception.Code.Should().Be(code);
        if (externalCode is not null)
        {
            exception.ExternalCode.Should().Be(externalCode);
        }

        if (kind is not null)
        {
            exception.Kind.Should().Be(kind.Value);
        }
    }
}
