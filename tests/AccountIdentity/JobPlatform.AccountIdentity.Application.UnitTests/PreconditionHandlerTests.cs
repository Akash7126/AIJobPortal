using JobPlatform.AccountIdentity.Application.Commands.Accounts;
using JobPlatform.AccountIdentity.Application.Commands.Administration;
using JobPlatform.AccountIdentity.Application.Interfaces;
using JobPlatform.AccountIdentity.Domain.Accounts;
using JobPlatform.AccountIdentity.Domain.Common;
using JobPlatform.AccountIdentity.Domain.Interfaces.Repositories;
using JobPlatform.AccountIdentity.Domain.PasswordPolicies;
using JobPlatform.AccountIdentity.Domain.Rbac;
using JobPlatform.AccountIdentity.Domain.Sessions;
using JobPlatform.SharedKernel.Application.Concurrency;
using JobPlatform.SharedKernel.Application.Results;
using JobPlatform.SharedKernel.Common.Enums;
using JobPlatform.SharedKernel.Security;
using JobPlatform.TestSupport;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;

namespace JobPlatform.AccountIdentity.Application.UnitTests;

/// <summary>Optimistic concurrency at the application boundary (foundation section 11): a stale If-Match is refused with 412, before the aggregate is touched.</summary>
public class PreconditionHandlerTests
{
    private const string Stale = "\"stale-version\"";

    private readonly FakeTimeProvider _clock = AppKit.Clock();
    private readonly Guid _adminId = Guid.NewGuid();

    private JobPlatform.SharedKernel.Application.Interfaces.Ports.ICurrentUser Admin() => AppKit.User(_adminId, ActorType.Administrator, mfa: true);

    private static void ShouldBePreconditionFailed<T>(Result<T> result)
    {
        var error = AppKit.ErrorOf(result);
        error.Type.Should().Be(ErrorType.PreconditionFailed);
        error.Code.Should().Be("E-PRECONDITION-FAILED");
    }

    [Fact]
    public async Task ConfigurePasswordPolicy_WithAStaleIfMatch_IsRefused_AndLeavesThePolicyUntouched()
    {
        var policy = PasswordPolicy.CreateDefault(_clock);
        var handlers = new RequestHandlerSet(ApplicationAssembly.Assembly, AppKit.PolicyRepo(_clock, policy), Substitute.For<IIdentityReadStore>(), Admin(), _clock);

        ShouldBePreconditionFailed(await handlers.Handle(new ConfigurePasswordPolicyCommand(12, true, true, true, Stale), default));

        policy.MinLength.Should().Be(8);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("*")]
    public async Task ConfigurePasswordPolicy_WithoutAPreconditionOrWithStar_IsApplied(string? ifMatch)
    {
        var policy = PasswordPolicy.CreateDefault(_clock);
        var handlers = new RequestHandlerSet(ApplicationAssembly.Assembly, AppKit.PolicyRepo(_clock, policy), Substitute.For<IIdentityReadStore>(), Admin(), _clock);

        (await handlers.Handle(new ConfigurePasswordPolicyCommand(12, true, true, true, ifMatch), default)).IsSuccess.Should().BeTrue();

        policy.MinLength.Should().Be(12);
    }

    [Fact]
    public async Task ConfigurePasswordPolicy_WithTheCurrentETag_IsApplied()
    {
        var policy = PasswordPolicy.CreateDefault(_clock);
        var handlers = new RequestHandlerSet(ApplicationAssembly.Assembly, AppKit.PolicyRepo(_clock, policy), Substitute.For<IIdentityReadStore>(), Admin(), _clock);

        (await handlers.Handle(new ConfigurePasswordPolicyCommand(12, true, true, true, ETag.From(policy.RowVersion)), default)).IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task ConfigureSessionTimeout_WithAStaleIfMatch_IsRefused()
    {
        var setting = SessionTimeoutSetting.CreateDefault(_clock);
        var handlers = new RequestHandlerSet(ApplicationAssembly.Assembly, AppKit.TimeoutRepo(_clock, setting), Substitute.For<IIdentityReadStore>(), Admin(), _clock);

        ShouldBePreconditionFailed(await handlers.Handle(new ConfigureSessionTimeoutCommand(60, Stale), default));

        setting.IdleTimeoutMinutes.Should().Be(30);
    }

    [Fact]
    public async Task GrantAndRevokePermission_WithAStaleIfMatch_AreRefused()
    {
        var role = Role.Create(RoleId.New(), "Custom", false, new[] { Permissions.JobsBrowse });
        var roles = Substitute.For<IRoleRepository>();
        roles.GetByIdAsync(role.Id, Arg.Any<CancellationToken>()).Returns(role);
        var handlers = new RequestHandlerSet(ApplicationAssembly.Assembly, roles, Substitute.For<IIdentityReadStore>(), Admin(), _clock);

        ShouldBePreconditionFailed(await handlers.Handle(new GrantPermissionCommand(role.Id.Value, Permissions.AccountsRead, Stale), default));
        ShouldBePreconditionFailed(await handlers.Handle(new RevokePermissionCommand(role.Id.Value, Permissions.JobsBrowse, Stale), default));

        role.Has(Permissions.AccountsRead).Should().BeFalse();
        role.Has(Permissions.JobsBrowse).Should().BeTrue();
    }

    [Fact]
    public async Task AdministratorAccountCommands_WithAStaleIfMatch_AreRefused_AndNothingChanges()
    {
        var accounts = new InMemoryAccounts();
        var sessions = new InMemorySessionStore();
        var account = AppKit.Active(_clock);
        accounts.Add(account);
        var handlers = new RequestHandlerSet(ApplicationAssembly.Assembly, accounts, sessions, Admin(), _clock);
        var id = account.Id.Value;

        var results = new[]
        {
            await handlers.Handle(new ApproveUserAccountCommand(id, Stale), default),
            await handlers.Handle(new BanUserAccountCommand(id, "x", Stale), default),
            await handlers.Handle(new DeactivateUserAccountCommand(id, "x", Stale), default),
            await handlers.Handle(new ResetCredentialsCommand(id, Stale), default),
            await handlers.Handle(new AssignRoleToAccountCommand(id, Guid.NewGuid(), Stale), default),
            await handlers.Handle(new RemoveRoleFromAccountCommand(id, Guid.NewGuid(), Stale), default)
        };

        results.Should().OnlyContain(r => r.IsFailure && r.Error!.Type == ErrorType.PreconditionFailed);
        account.Standing.Should().Be(AccountStanding.Active);
        account.MustChangePassword.Should().BeFalse();
        sessions.InvalidatedAccounts.Should().BeEmpty("a refused precondition has no side effects");
    }

    [Fact]
    public async Task AdministratorAccountCommand_WithTheCurrentETag_IsApplied()
    {
        var accounts = new InMemoryAccounts();
        var account = AppKit.Active(_clock);
        accounts.Add(account);
        var handlers = new RequestHandlerSet(ApplicationAssembly.Assembly, accounts, new InMemorySessionStore(), Admin(), _clock);

        (await handlers.Handle(new BanUserAccountCommand(account.Id.Value, "fraud", ETag.From(account.RowVersion)), default)).IsSuccess.Should().BeTrue();

        account.Standing.Should().Be(AccountStanding.Banned);
    }
}
