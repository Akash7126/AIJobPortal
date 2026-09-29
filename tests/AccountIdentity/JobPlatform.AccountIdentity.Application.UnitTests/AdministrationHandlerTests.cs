using JobPlatform.AccountIdentity.Application.Abstractions;
using JobPlatform.AccountIdentity.Application.Commands.Accounts;
using JobPlatform.AccountIdentity.Application.Commands.Administration;
using JobPlatform.AccountIdentity.Application.Commands.Consent;
using JobPlatform.AccountIdentity.Application.DTOs.Accounts;
using JobPlatform.AccountIdentity.Application.DTOs.Administration;
using JobPlatform.AccountIdentity.Application.DTOs.Consent;
using JobPlatform.AccountIdentity.Application.Events;
using JobPlatform.AccountIdentity.Application.Handlers.Accounts;
using JobPlatform.AccountIdentity.Application.Queries.Accounts;
using JobPlatform.AccountIdentity.Application.Queries.Administration;
using JobPlatform.AccountIdentity.Application.Queries.Consent;
using JobPlatform.AccountIdentity.Application.Queries.Internal;
using JobPlatform.AccountIdentity.Application.Security;
using JobPlatform.AccountIdentity.Domain.Accounts;
using JobPlatform.AccountIdentity.Domain.ApiCredentials;
using JobPlatform.AccountIdentity.Domain.Common;
using JobPlatform.AccountIdentity.Domain.Consent;
using JobPlatform.AccountIdentity.Domain.PasswordPolicies;
using JobPlatform.AccountIdentity.Domain.Rbac;
using JobPlatform.AccountIdentity.Domain.Sessions;
using JobPlatform.SharedKernel.ApiContracts.AccountIdentity;
using JobPlatform.SharedKernel.Application.Paging;
using JobPlatform.SharedKernel.Application.Results;
using JobPlatform.SharedKernel.Common.Enums;
using JobPlatform.SharedKernel.Domain;
using JobPlatform.SharedKernel.IntegrationEvents.AccountIdentity;
using JobPlatform.SharedKernel.Messaging;
using JobPlatform.SharedKernel.Security;
using JobPlatform.TestSupport;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;

namespace JobPlatform.AccountIdentity.Application.UnitTests;

public class AdminAccountHandlerTests
{
    private readonly FakeTimeProvider _clock = AppKit.Clock();
    private readonly InMemoryAccounts _accounts = new();
    private readonly InMemorySessionStore _sessions = new();
    private readonly Guid _adminId = Guid.NewGuid();

    private RequestHandlerSet Handlers() => new(ApplicationAssembly.Assembly, _accounts, _sessions, AppKit.User(_adminId, ActorType.Administrator, mfa: true), _clock);

    private Account Add(Account account)
    {
        _accounts.Add(account);
        return account;
    }

    [Fact]
    [Trait("Story", "US-3.1.4-03")]
    [Trait("AC", "AC-01")]
    public async Task Approve_ActivatesAPendingAccount_AsTheCurrentAdministrator()
    {
        var account = Add(AppKit.Pending(_clock));

        var result = await Handlers().Handle(new ApproveUserAccountCommand(account.Id.Value), default);

        result.IsSuccess.Should().BeTrue();
        account.Standing.Should().Be(AccountStanding.Active);
        account.DomainEvents.OfType<UserAccountStandingChangedDomainEvent>().Single().ActorId.Should().Be(_adminId);
        _sessions.InvalidatedAccounts.Should().BeEmpty("approval does not end sessions");
    }

    [Fact]
    [Trait("Story", "US-3.1.4-03")]
    [Trait("AC", "AC-03")]
    public async Task Approve_BannedAccount_ThrowsTheBannedRule()
    {
        var account = Add(AppKit.Active(_clock));
        account.Ban(AppKit.Admin(), "x", _clock);

        var act = () => Handlers().Handle(new ApproveUserAccountCommand(account.Id.Value), default);

        (await act.Should().ThrowAsync<BusinessRuleViolationException>()).Which.ExternalCode.Should().Be(ErrorCodes.AdminStateBanned);
    }

    [Fact]
    [Trait("Story", "US-3.1.4-03")]
    [Trait("AC", "AC-01")]
    public async Task Ban_Deactivate_ResetCredentials_EndEveryLiveSessionOfTheAccount()
    {
        var banned = Add(AppKit.Active(_clock, mobile: "+970591111111"));
        var deactivated = Add(AppKit.Active(_clock, mobile: "+970592222222"));
        var reset = Add(AppKit.Active(_clock, mobile: "+970593333333"));

        (await Handlers().Handle(new BanUserAccountCommand(banned.Id.Value, "fraud"), default)).IsSuccess.Should().BeTrue();
        (await Handlers().Handle(new DeactivateUserAccountCommand(deactivated.Id.Value, "pause"), default)).IsSuccess.Should().BeTrue();
        (await Handlers().Handle(new ResetCredentialsCommand(reset.Id.Value), default)).IsSuccess.Should().BeTrue();

        banned.Standing.Should().Be(AccountStanding.Banned);
        deactivated.Standing.Should().Be(AccountStanding.Deactivated);
        reset.MustChangePassword.Should().BeTrue();
        _sessions.InvalidatedAccounts.Should().BeEquivalentTo(new[] { banned.Id.Value, deactivated.Id.Value, reset.Id.Value });
    }

    [Fact]
    public async Task RoleAssignment_AddsAndRemovesTheRoleOnTheAccount()
    {
        var account = Add(AppKit.Active(_clock));
        var role = RoleId.New();

        await Handlers().Handle(new AssignRoleToAccountCommand(account.Id.Value, role.Value), default);
        account.RoleAssignments.Should().Contain(r => r.RoleId == role);
        await Handlers().Handle(new RemoveRoleFromAccountCommand(account.Id.Value, role.Value), default);

        account.RoleAssignments.Should().NotContain(r => r.RoleId == role);
    }

    [Fact]
    public async Task EveryAdministratorCommand_ReturnsNotFoundForAnUnknownAccount()
    {
        var id = Guid.NewGuid();
        var handlers = Handlers();
        var results = new[]
        {
            await handlers.Handle(new ApproveUserAccountCommand(id), default),
            await handlers.Handle(new BanUserAccountCommand(id, "x"), default),
            await handlers.Handle(new DeactivateUserAccountCommand(id, "x"), default),
            await handlers.Handle(new ResetCredentialsCommand(id), default),
            await handlers.Handle(new AssignRoleToAccountCommand(id, id), default),
            await handlers.Handle(new RemoveRoleFromAccountCommand(id, id), default)
        };

        results.Should().OnlyContain(r => r.IsFailure && r.Error!.Type == ErrorType.NotFound && r.Error.Code == ErrorCodes.NotFound);
    }

    [Fact]
    [Trait("Story", "US-3.1.4-03")]
    [Trait("AC", "AC-04")]
    public void AdministratorCommands_DeclareAdministratorMfaPermissionAndTheAumForbiddenCode()
    {
        var expected = new (object Command, string Permission)[]
        {
            (new ApproveUserAccountCommand(Guid.NewGuid()), Permissions.AccountsApprove),
            (new BanUserAccountCommand(Guid.NewGuid(), "x"), Permissions.AccountsBan),
            (new DeactivateUserAccountCommand(Guid.NewGuid(), "x"), Permissions.AccountsDeactivate),
            (new ResetCredentialsCommand(Guid.NewGuid()), Permissions.AccountsResetCredentials),
            (new AssignRoleToAccountCommand(Guid.NewGuid(), Guid.NewGuid()), Permissions.AccountsAssignRoles),
            (new GetAccountStandingQuery(Guid.NewGuid()), Permissions.AccountsRead),
            (new ListAccountsQuery(null, null, null), Permissions.AccountsRead)
        };

        foreach (var (command, permission) in expected)
        {
            var authorized = command.Should().BeAssignableTo<JobPlatform.SharedKernel.Application.Abstractions.IAuthorizedRequest>().Which;
            authorized.RequiredPermission.Should().Be(permission);
            authorized.AllowedActorTypes.Should().Equal(ActorType.Administrator);
            authorized.RequireMfa.Should().BeTrue();
            authorized.ForbiddenErrorCode.Should().Be(ErrorCodes.AdminForbidden);
        }
    }

    [Fact]
    public async Task RequestAccountDeactivation_FromAService_DeactivatesAndEndsSessions()
    {
        var account = Add(AppKit.Active(_clock));
        var handler = new RequestAccountDeactivationHandler(_accounts, _sessions, _clock);

        var result = await handler.Handle(new RequestAccountDeactivationCommand(account.Id.Value, "Delete", "privacy setting"), default);

        result.Value.Standing.Should().Be("DeletionRequested");
        account.Standing.Should().Be(AccountStanding.Deactivated);
        account.DomainEvents.OfType<AccountSuspendedDomainEvent>().Single().Kind.Should().Be(SuspensionKind.DeletionRequested);
        _sessions.InvalidatedAccounts.Should().Contain(account.Id.Value);
        AppKit.ErrorOf(await handler.Handle(new RequestAccountDeactivationCommand(Guid.NewGuid(), "Delete", "x"), default)).Type.Should().Be(ErrorType.NotFound);
        var again = () => handler.Handle(new RequestAccountDeactivationCommand(account.Id.Value, "Deactivate", "again"), default);
        (await again.Should().ThrowAsync<BusinessRuleViolationException>()).Which.Code.Should().Be(AccountRuleCodes.AlreadyDeactivated);
    }

    [Theory]
    [InlineData("Pending", new[] { "approve", "ban", "reset-credentials", "monitor" })]
    [InlineData("Active", new[] { "ban", "deactivate", "reset-credentials", "monitor" })]
    [InlineData("Deactivated", new[] { "approve", "ban", "reset-credentials", "monitor" })]
    [InlineData("Banned", new[] { "reset-credentials", "monitor" })]
    [Trait("Story", "US-3.1.4-03")]
    [Trait("AC", "AC-02")]
    public void AvailableActions_FollowTheAccountStanding(string standing, string[] expected) =>
        GetAccountStandingHandler.ActionsFor(standing).Should().Equal(expected);

    [Fact]
    public async Task AccountQueries_MaskPersonalData_AndReportNotFound()
    {
        var store = Substitute.For<IIdentityReadStore>();
        var id = Guid.NewGuid();
        store.GetAccountStandingAsync(id, Arg.Any<CancellationToken>()).Returns(new AccountStandingView(id, ActorType.JobSeeker, "Sara", "sara@example.com",
            "+970591111111", "Active", false, null, false, true, false, DateTime.UtcNow, null, Array.Empty<AccountStatusChangeView>(), Array.Empty<string>()));
        store.ListAccountsAsync(Arg.Any<AccountListFilter>(), Arg.Any<PageRequest>(), Arg.Any<CancellationToken>()).Returns(new PagedResult<AccountListItemView>(
            new[] { new AccountListItemView(id, ActorType.JobSeeker, "Sara", "sara@example.com", "+970591111111", "Active", DateTime.UtcNow) }, 1, 20, 1));
        store.GetAccountSummaryAsync(id, Arg.Any<CancellationToken>()).Returns(new AccountSummaryDto(id, ActorType.JobSeeker, "Active", DateTime.UtcNow, null));
        var handlers = new RequestHandlerSet(ApplicationAssembly.Assembly, store);

        var standing = (await handlers.Handle(new GetAccountStandingQuery(id), default)).Value;
        var list = (await handlers.Handle(new ListAccountsQuery(null, null, null), default)).Value;

        standing.Mobile.Should().Be("+970****111").And.NotContain("591111");
        standing.Email.Should().Be("s***@example.com");
        standing.AvailableActions.Should().Contain("deactivate");
        list.Items.Single().Mobile.Should().Contain("****");
        list.Items.Single().Email.Should().Contain("***");
        (await handlers.Handle(new GetAccountSummaryQuery(id), default)).Value.Standing.Should().Be("Active");
        AppKit.ErrorOf(await handlers.Handle(new GetAccountStandingQuery(Guid.NewGuid()), default)).Type.Should().Be(ErrorType.NotFound);
        AppKit.ErrorOf(await handlers.Handle(new GetAccountSummaryQuery(Guid.NewGuid()), default)).Type.Should().Be(ErrorType.NotFound);
    }

    [Fact]
    public void Masking_HandlesShortAndMissingValues()
    {
        Masking.Email(null).Should().BeNull();
        Masking.Email("a@b.com").Should().Be("***@b.com");
        Masking.Mobile("+1234").Should().Be("****");
    }
}

public class ConfigurationHandlerTests
{
    private readonly FakeTimeProvider _clock = AppKit.Clock();
    private readonly Guid _adminId = Guid.NewGuid();

    private JobPlatform.SharedKernel.Application.Ports.ICurrentUser Admin() => AppKit.User(_adminId, ActorType.Administrator, mfa: true);

    [Fact]
    [Trait("Story", "US-3.1.5-02")]
    [Trait("AC", "AC-03")]
    public async Task ConfigurePasswordPolicy_UpdatesTheSingletonAndBumpsItsVersion()
    {
        var policy = PasswordPolicy.CreateDefault(_clock);
        var handlers = new RequestHandlerSet(ApplicationAssembly.Assembly, AppKit.PolicyRepo(_clock, policy), Substitute.For<IIdentityReadStore>(), Admin(), _clock);

        var result = await handlers.Handle(new ConfigurePasswordPolicyCommand(12, true, false, true), default);

        result.IsSuccess.Should().BeTrue();
        policy.MinLength.Should().Be(12);
        policy.RequireLower.Should().BeFalse();
        policy.PolicyVersion.Should().Be(2);
        policy.UpdatedBy.Should().Be(_adminId);
    }

    [Fact]
    public async Task GetPasswordPolicy_ReadsTheProjection()
    {
        var store = Substitute.For<IIdentityReadStore>();
        var view = new PasswordPolicyView(8, true, true, true, 1, DateTime.UtcNow);
        store.GetPasswordPolicyAsync(Arg.Any<CancellationToken>()).Returns(view);
        var handlers = new RequestHandlerSet(ApplicationAssembly.Assembly, AppKit.PolicyRepo(_clock), store, Admin(), _clock);

        (await handlers.Handle(new GetPasswordPolicyQuery(), default)).Value.Should().Be(view);
    }

    [Fact]
    [Trait("Story", "US-3.1.5-04")]
    [Trait("AC", "AC-03")]
    public async Task ConfigureSessionTimeout_UpdatesTheSettingUsedByFutureSessions()
    {
        var setting = SessionTimeoutSetting.CreateDefault(_clock);
        var store = Substitute.For<IIdentityReadStore>();
        store.GetSessionTimeoutAsync(Arg.Any<CancellationToken>()).Returns(new SessionTimeoutView(60, 2, DateTime.UtcNow));
        var handlers = new RequestHandlerSet(ApplicationAssembly.Assembly, AppKit.TimeoutRepo(_clock, setting), store, Admin(), _clock);

        (await handlers.Handle(new ConfigureSessionTimeoutCommand(60), default)).IsSuccess.Should().BeTrue();

        setting.IdleTimeoutMinutes.Should().Be(60);
        (await handlers.Handle(new GetSessionTimeoutQuery(), default)).Value.IdleTimeoutMinutes.Should().Be(60);
    }

    [Fact]
    [Trait("Story", "US-3.1.5-03")]
    [Trait("AC", "AC-04")]
    public async Task RoleHandlers_GrantRevokeAndListPermissions()
    {
        var role = Role.Create(RoleId.New(), "Custom", false, new[] { Permissions.JobsBrowse });
        var roles = Substitute.For<IRoleRepository>();
        roles.GetByIdAsync(role.Id, Arg.Any<CancellationToken>()).Returns(role);
        var store = Substitute.For<IIdentityReadStore>();
        store.ListRolesAsync(Arg.Any<CancellationToken>()).Returns(new[] { new RoleView(role.Id.Value, "Custom", false, new[] { Permissions.JobsBrowse }) });
        var handlers = new RequestHandlerSet(ApplicationAssembly.Assembly, roles, store, Admin(), _clock);

        await handlers.Handle(new GrantPermissionCommand(role.Id.Value, Permissions.AccountsRead), default);
        role.Has(Permissions.AccountsRead).Should().BeTrue();
        await handlers.Handle(new RevokePermissionCommand(role.Id.Value, Permissions.JobsBrowse), default);
        role.Has(Permissions.JobsBrowse).Should().BeFalse();

        (await handlers.Handle(new ListRolesQuery(), default)).Value.Should().ContainSingle();
        AppKit.ErrorOf(await handlers.Handle(new GrantPermissionCommand(Guid.NewGuid(), Permissions.JobsBrowse), default)).Type.Should().Be(ErrorType.NotFound);
        AppKit.ErrorOf(await handlers.Handle(new RevokePermissionCommand(Guid.NewGuid(), Permissions.JobsBrowse), default)).Type.Should().Be(ErrorType.NotFound);
    }

    [Fact]
    public async Task RoleDomainEvents_EvictTheCaches()
    {
        var cache = Substitute.For<ICacheInvalidator>();
        var accountId = AccountId.New();

        await new RolePermissionsChangedHandler(cache).Handle(new RolePermissionsChangedDomainEvent(RoleId.New(), DateTime.UtcNow), default);
        await new AccountRolesChangedHandler(cache).Handle(new AccountRolesChangedDomainEvent(accountId, DateTime.UtcNow), default);
        await new PasswordPolicyConfiguredHandler(cache).Handle(new PasswordPolicyConfiguredDomainEvent(2, DateTime.UtcNow), default);

        await cache.Received(1).InvalidateRoleMapAsync(Arg.Any<CancellationToken>());
        await cache.Received(1).InvalidateAccountRolesAsync(accountId.Value, Arg.Any<CancellationToken>());
        await cache.Received(1).InvalidatePasswordPolicyAsync(Arg.Any<CancellationToken>());
    }

    // ------------------------------------------------------------------ consent

    private static IOptions<ConsentOptions> Options() => Microsoft.Extensions.Options.Options.Create(new ConsentOptions { CurrentPolicyVersion = "v1" });

    [Fact]
    [Trait("Story", "US-4.1-04")]
    [Trait("AC", "AC-05")]
    public async Task RecordConsent_CreatesTheFirstDecision_AndUpdatesTheExistingOneOnTheSecondCall()
    {
        var repo = Substitute.For<IPrivacyConsentRepository>();
        PrivacyConsent? added = null;
        repo.When(r => r.Add(Arg.Any<PrivacyConsent>())).Do(ci => added = ci.Arg<PrivacyConsent>());
        var handlers = new RequestHandlerSet(ApplicationAssembly.Assembly, repo, Substitute.For<IIdentityReadStore>(), Options(), _clock);
        var guest = Guid.NewGuid();

        var first = await handlers.Handle(new RecordPrivacyConsentCommand(guest, "v1", true, false, false, "ar", null), default);
        repo.GetAsync(guest, "v1", Arg.Any<CancellationToken>()).Returns(added);
        var second = await handlers.Handle(new RecordPrivacyConsentCommand(guest, "v1", true, true, false, "en", null), default);

        first.Value.BannerRequired.Should().BeFalse();
        first.Value.Allowed.Should().Be(new ConsentChoicesDto(true, true, false, false));
        repo.Received(1).Add(Arg.Any<PrivacyConsent>());
        added!.Choices.Preferences.Should().BeTrue("the second call updated the same aggregate");
        second.Value.Allowed.Preferences.Should().BeTrue();
    }

    [Fact]
    public async Task RecordConsent_WithoutGuestId_MintsOne()
    {
        var handlers = new RequestHandlerSet(ApplicationAssembly.Assembly, Substitute.For<IPrivacyConsentRepository>(), Substitute.For<IIdentityReadStore>(), Options(), _clock);

        var result = await handlers.Handle(new RecordPrivacyConsentCommand(null, "v1", false, false, false, null, null), default);

        result.Value.GuestId.Should().NotBeNull().And.NotBe(Guid.Empty);
    }

    [Fact]
    [Trait("Story", "US-4.1-04")]
    [Trait("AC", "AC-04")]
    public async Task GetCurrentConsent_WithoutADecision_WithholdsNonEssentialCollectionAndRequiresTheBanner()
    {
        var store = Substitute.For<IIdentityReadStore>();
        var handlers = new RequestHandlerSet(ApplicationAssembly.Assembly, Substitute.For<IPrivacyConsentRepository>(), store, Options(), _clock);

        var anonymous = (await handlers.Handle(new GetCurrentConsentQuery(null), default)).Value;
        var unknownGuest = (await handlers.Handle(new GetCurrentConsentQuery(Guid.NewGuid()), default)).Value;

        foreach (var status in new[] { anonymous, unknownGuest })
        {
            status.BannerRequired.Should().BeTrue();
            status.Allowed.Should().Be(new ConsentChoicesDto(true, false, false, false));
            status.CurrentPolicyVersion.Should().Be("v1");
            status.BannerText.Ar.Should().NotBeNullOrEmpty();
            status.BannerText.En.Should().NotBeNullOrEmpty();
        }
    }

    [Fact]
    [Trait("Story", "US-4.1-04")]
    [Trait("AC", "AC-05")]
    public async Task GetCurrentConsent_WithADecision_HidesTheBannerAndReturnsTheChoices()
    {
        var store = Substitute.For<IIdentityReadStore>();
        var guest = Guid.NewGuid();
        store.GetConsentAsync(guest, "v1", Arg.Any<CancellationToken>()).Returns(new ConsentDecisionView(guest, "v1", true, false, true, DateTime.UtcNow, "En"));
        var handlers = new RequestHandlerSet(ApplicationAssembly.Assembly, Substitute.For<IPrivacyConsentRepository>(), store, Options(), _clock);

        var status = (await handlers.Handle(new GetCurrentConsentQuery(guest), default)).Value;

        status.BannerRequired.Should().BeFalse();
        status.Allowed.Should().Be(new ConsentChoicesDto(true, true, false, true));
    }

    // ------------------------------------------------------------------ internal queries

    [Fact]
    public async Task InternalQueries_ReadAccessLogAndCheckPermissionsAgainstTheRoleDirectory()
    {
        var store = Substitute.For<IIdentityReadStore>();
        store.ListAccessLogAsync(null, null, null, Arg.Any<PageRequest>(), Arg.Any<CancellationToken>())
            .Returns(new PagedResult<AccessLogEntryDto>(Array.Empty<AccessLogEntryDto>(), 1, 50, 0));
        var roles = Substitute.For<IRoleDirectory>();
        var accountId = Guid.NewGuid();
        roles.GetRolesForAccountAsync(accountId, Arg.Any<CancellationToken>()).Returns(new[]
        {
            Role.Create(RoleId.New(), "X", false, new[] { Permissions.JobsBrowse }).ToRolePermissions()
        });
        var handlers = new RequestHandlerSet(ApplicationAssembly.Assembly, store, roles);

        (await handlers.Handle(new ListAccessLogQuery(null, null, null), default)).Value.TotalCount.Should().Be(0);
        (await handlers.Handle(new CheckPermissionQuery(accountId, Permissions.JobsBrowse), default)).Value.Allowed.Should().BeTrue();
        (await handlers.Handle(new CheckPermissionQuery(accountId, Permissions.AccountsBan), default)).Value.Allowed.Should().BeFalse();
    }
}

public class AccessAuthorizerAndMapperTests
{
    private readonly FakeTimeProvider _clock = AppKit.Clock();
    private readonly IRoleDirectory _roles = Substitute.For<IRoleDirectory>();
    private readonly RecordingAccessLog _log = new();

    private AccessAuthorizer Authorizer() => new(_roles, _log, _clock);

    private sealed record NeedsPermission(string Permission, bool Mfa = false, ActorType[]? Actors = null) : JobPlatform.SharedKernel.Application.Abstractions.IAuthorizedRequest
    {
        public IReadOnlyCollection<ActorType> AllowedActorTypes => Actors ?? Array.Empty<ActorType>();
        public string? RequiredPermission => Permission;
        public bool RequireMfa => Mfa;
    }

    [Fact]
    [Trait("Story", "US-3.1.5-03")]
    [Trait("AC", "AC-01")]
    public async Task Authorize_WhenARoleHoldsThePermission_AllowsAndLogsTheDecision()
    {
        var id = Guid.NewGuid();
        _roles.GetRolesForAccountAsync(id, Arg.Any<CancellationToken>()).Returns(new[] { Role.Create(RoleId.New(), "R", false, new[] { Permissions.RolesRead }).ToRolePermissions() });

        var result = await Authorizer().AuthorizeAsync(AppKit.User(id, ActorType.Administrator, mfa: true), new NeedsPermission(Permissions.RolesRead), "ListRolesQuery");

        result.IsSuccess.Should().BeTrue();
        _log.Entries.Should().ContainSingle(e => e.Decision == "Allow" && e.Action == "ListRolesQuery" && e.Resource == Permissions.RolesRead && e.AccountId == id);
    }

    [Fact]
    [Trait("Story", "US-3.1.5-03")]
    [Trait("AC", "AC-02")]
    public async Task Authorize_WhenNoRoleHoldsThePermission_DeniesWithTheRequestsForbiddenCode_AndLogsIt()
    {
        var id = Guid.NewGuid();
        _roles.GetRolesForAccountAsync(id, Arg.Any<CancellationToken>()).Returns(Array.Empty<RolePermissions>());

        var result = await Authorizer().AuthorizeAsync(AppKit.User(id, ActorType.JobSeeker), new NeedsPermission(Permissions.AccountsBan), "BanUserAccountCommand");

        var error = AppKit.ErrorOf(result);
        error.Code.Should().Be(ErrorCodes.AuthForbidden);
        error.Type.Should().Be(ErrorType.Forbidden);
        _log.Entries.Should().ContainSingle(e => e.Decision == "Deny" && e.Reason!.Contains(Permissions.AccountsBan));
    }

    [Fact]
    public async Task Authorize_UnauthenticatedCaller_Returns401_AndNothingIsLogged()
    {
        var result = await Authorizer().AuthorizeAsync(AppKit.User(authenticated: false), new NeedsPermission("x"), "Anything");

        AppKit.ErrorOf(result).Type.Should().Be(ErrorType.Unauthorized);
        _log.Entries.Should().BeEmpty();
    }

    [Fact]
    public async Task Authorize_WrongActorTypeOrMissingMfa_IsDeniedBeforeAnyPermissionLookup()
    {
        var id = Guid.NewGuid();

        var actor = await Authorizer().AuthorizeAsync(AppKit.User(id, ActorType.JobSeeker), new NeedsPermission("x", Actors: new[] { ActorType.Administrator }), "A");
        var mfa = await Authorizer().AuthorizeAsync(AppKit.User(id, ActorType.Administrator, mfa: false), new NeedsPermission("x", Mfa: true), "B");

        AppKit.ErrorOf(actor).Type.Should().Be(ErrorType.Forbidden);
        AppKit.ErrorOf(mfa).Type.Should().Be(ErrorType.Forbidden);
        _log.Entries.Select(e => e.Reason).Should().Equal("actor-type-not-allowed", "mfa-required");
        await _roles.DidNotReceiveWithAnyArgs().GetRolesForAccountAsync(default, default);
    }

    [Fact]
    public async Task Authorize_ServiceTokens_AreScopeBased_AndNeedNoRolesOrAccountId()
    {
        var withScope = AppKit.User(null, ActorType.System, scopes: new[] { Scopes.Internal });
        var withoutScope = AppKit.User(null, ActorType.System, scopes: new[] { "other" });

        (await Authorizer().AuthorizeAsync(withScope, new NeedsPermission(Permissions.AccessLogRead), "ListAccessLogQuery")).IsSuccess.Should().BeTrue();
        AppKit.ErrorOf(await Authorizer().AuthorizeAsync(withoutScope, new NeedsPermission(Permissions.AccessLogRead), "ListAccessLogQuery")).Type.Should().Be(ErrorType.Forbidden);
        await _roles.DidNotReceiveWithAnyArgs().GetRolesForAccountAsync(default, default);
    }

    // ------------------------------------------------------------------ integration-event mapping

    private static readonly DomainEventContext Context = new("agg-1", 7, Guid.NewGuid(), Guid.NewGuid());
    private readonly AccountIdentityEventMapper _mapper = new();

    [Fact]
    [Trait("Story", "US-3.1.1-01")]
    [Trait("AC", "AC-04")]
    public void Mapper_AccountRegistered_BecomesAccountCreatedWithActorType()
    {
        var id = AccountId.New();

        var mapped = _mapper.Map(new AccountRegisteredDomainEvent(id, ActorType.Employer, DateTime.UtcNow), Context)
            .Should().BeOfType<AccountCreatedIntegrationEvent>().Which;

        mapped.AccountId.Should().Be(id.Value);
        mapped.ActorType.Should().Be(ActorType.Employer);
        mapped.AggregateVersion.Should().Be(7);
        mapped.CorrelationId.Should().Be(Context.CorrelationId);
        mapped.CausationId.Should().Be(Context.CausationId);
        mapped.RoutingKey.Should().Be("account.created.v1");
        mapped.Exchange.Should().Be("jobplatform.account-identity.events");
        mapped.Version.Should().Be(1);
    }

    [Fact]
    [Trait("Story", "US-3.1.1-02")]
    [Trait("AC", "AC-04")]
    public void Mapper_AccountActivated_BecomesAccountApprovedWithActorType()
    {
        var id = AccountId.New();
        var actor = Guid.NewGuid();

        var mapped = _mapper.Map(new AccountActivatedDomainEvent(id, ActorType.JobSeeker, actor, DateTime.UtcNow), Context)
            .Should().BeOfType<AccountApprovedIntegrationEvent>().Which;

        mapped.ActorType.Should().Be(ActorType.JobSeeker);
        mapped.ActorId.Should().Be(actor);
        mapped.RoutingKey.Should().Be("account.approved.v1");
    }

    [Fact]
    [Trait("Story", "US-3.1.4-03")]
    [Trait("AC", "AC-05")]
    public void Mapper_StandingChange_BecomesUserAccountApprovedWithFromAndTo()
    {
        var id = AccountId.New();

        var mapped = _mapper.Map(new UserAccountStandingChangedDomainEvent(id, AccountStanding.Pending, AccountStanding.Active, Guid.NewGuid(), "r", DateTime.UtcNow), Context)
            .Should().BeOfType<UserAccountApprovedIntegrationEvent>().Which;

        mapped.UserAccountId.Should().Be(id.Value);
        mapped.FromStanding.Should().Be("Pending");
        mapped.ToStanding.Should().Be("Active");
        mapped.RoutingKey.Should().Be("user-account.approved.v1");
    }

    [Fact]
    public void Mapper_AccountSuspended_CarriesReasonAndStanding()
    {
        var mapped = _mapper.Map(new AccountSuspendedDomainEvent(AccountId.New(), ActorType.JobSeeker, Guid.NewGuid(), "privacy", SuspensionKind.DeletionRequested, DateTime.UtcNow), Context)
            .Should().BeOfType<AccountSuspendedIntegrationEvent>().Which;

        mapped.Reason.Should().Be("privacy");
        mapped.Standing.Should().Be("DeletionRequested");
        mapped.RoutingKey.Should().Be("account.suspended.v1");
    }

    [Fact]
    [Trait("Story", "US-3.1.3-02")]
    [Trait("AC", "AC-01")]
    public void Mapper_ApiCredentialIssued_CarriesExpiryAndNoSecretMaterial()
    {
        var expires = DateTime.UtcNow.AddDays(30);

        var mapped = _mapper.Map(new ApiCredentialIssuedDomainEvent(ApiCredentialId.New(), AccountId.New(), Guid.NewGuid(), expires, DateTime.UtcNow), Context)
            .Should().BeOfType<ApiCredentialCreatedIntegrationEvent>().Which;

        mapped.ExpiresAtUtc.Should().Be(expires);
        mapped.RoutingKey.Should().Be("api-credential.created.v1");
        typeof(ApiCredentialCreatedIntegrationEvent).GetProperties().Select(p => p.Name).Should().NotContain(n => n.Contains("Secret") || n.Contains("Hash"));
    }

    [Fact]
    public void Mapper_InternalOnlyEvents_ProduceNoIntegrationEvent()
    {
        _mapper.Map(new CredentialsResetDomainEvent(AccountId.New(), Guid.NewGuid(), DateTime.UtcNow), Context).Should().BeNull();
        _mapper.Map(new AccountRolesChangedDomainEvent(AccountId.New(), DateTime.UtcNow), Context).Should().BeNull();
        _mapper.Map(new RolePermissionsChangedDomainEvent(RoleId.New(), DateTime.UtcNow), Context).Should().BeNull();
        _mapper.Map(new PasswordPolicyConfiguredDomainEvent(2, DateTime.UtcNow), Context).Should().BeNull();
        _mapper.Map(new ApiCredentialRevokedDomainEvent(ApiCredentialId.New(), AccountId.New(), Guid.NewGuid(), DateTime.UtcNow), Context).Should().BeNull();
    }

    [Fact]
    public void IntegrationEvents_SerializeToCamelCaseJson_WithoutTransportMetadata()
    {
        var mapped = (AccountCreatedIntegrationEvent)_mapper.Map(new AccountRegisteredDomainEvent(AccountId.New(), ActorType.JobSeeker, DateTime.UtcNow), Context)!;

        var json = IntegrationJson.Serialize(mapped);

        json.Should().Contain("\"accountId\"").And.Contain("\"actorType\":\"JobSeeker\"").And.Contain("\"aggregateVersion\":7").And.Contain("\"messageId\"");
        json.Should().NotContain("routingKey").And.NotContain("exchange").And.NotContain("eventType");
        System.Text.Json.JsonSerializer.Deserialize<AccountCreatedIntegrationEvent>(json, IntegrationJson.Options)!.AccountId.Should().Be(mapped.AccountId);
        IntegrationJson.ToEnvelope(mapped).ToHeaders().Should().ContainKey("message-id").And.ContainKey("producer").And.ContainKey("correlation-id");
    }
}
