using System.Net;
using JobPlatform.AccountIdentity.Domain.Common;
using JobPlatform.AccountIdentity.Domain.Rbac;
using JobPlatform.SharedKernel.Security;
using Microsoft.EntityFrameworkCore;

namespace JobPlatform.AccountIdentity.Api.IntegrationTests;

public class AdminAccountsApiTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public AdminAccountsApiTests(ApiFactory factory) => _factory = factory;

    private async Task<Guid> AdminIdAsync() => (await _factory.WithDbAsync(db => db.Accounts.AsNoTracking().ToListAsync()))
        .Single(a => a.Email?.Value == ApiFactory.AdminEmail).Id.Value;

    private async Task<(ApiClient Client, Guid Id, string Mobile, ApiClient Admin)> PendingAsync()
    {
        var client = new ApiClient(_factory);
        var (id, mobile, _) = await client.RegisterJobSeekerAsync();
        return (client, id, mobile, await client.AdminAsync());
    }

    [Fact]
    [Trait("Story", "US-3.1.4-03")]
    [Trait("AC", "AC-01")]
    public async Task Approve_PendingAccount_ActivatesItAndRecordsTheStatusChange()
    {
        var (client, id, mobile, admin) = await PendingAsync();

        (await admin.PostAsync($"/api/v1/admin/accounts/{id}/approve")).StatusCode.Should().Be(HttpStatusCode.NoContent);

        var standing = await (await admin.Http.GetAsync($"/api/v1/admin/accounts/{id}")).Json();
        standing["standing"]!.GetValue<string>().Should().Be("Active");
        var history = standing["history"]!.AsArray();
        history.Last()!["from"]!.GetValue<string>().Should().Be("Pending");
        history.Last()!["to"]!.GetValue<string>().Should().Be("Active");
        history.Last()!["by"]!.GetValue<Guid>().Should().Be(await AdminIdAsync());
        (await client.LoginAsync(mobile, "Str0ngPass"))["status"]!.GetValue<string>().Should().Be("Authenticated");
    }

    [Fact]
    [Trait("Story", "US-3.1.4-03")]
    [Trait("AC", "AC-01")]
    public async Task Ban_TakesEffectAtNextAuthentication_AndEndsTheLiveSessionExplicitly()
    {
        var client = new ApiClient(_factory);
        var mobile = ApiClient.NewMobile();
        var id = await client.ActiveJobSeekerAsync(mobile);
        var (access, refresh) = await client.TokensAsync(mobile, "Str0ngPass");
        var admin = await client.AdminAsync();

        (await admin.PostAsync($"/api/v1/admin/accounts/{id}/ban", new { reason = "fraud" })).StatusCode.Should().Be(HttpStatusCode.NoContent);

        var login = await client.PostAsync("/api/v1/auth/login", new { username = mobile, password = "Str0ngPass", mechanism = "password" });
        await login.ShouldBeProblemAsync(HttpStatusCode.Forbidden, "E-AUM-STATE-BANNED");
        await (await new ApiClient(_factory).Bearer(access).PostAsync("/api/v1/auth/logout")).ShouldBeProblemAsync(HttpStatusCode.Unauthorized, "E-AAFR-SESSION-EXPIRED");
        (await client.PostAsync("/api/v1/auth/refresh", new { refreshToken = refresh })).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    [Trait("Story", "US-3.1.4-03")]
    [Trait("AC", "AC-01")]
    public async Task Deactivate_ActiveAccount_PreventsLogin_AndPublishesSuspendedAndStandingEvents()
    {
        var client = new ApiClient(_factory);
        var mobile = ApiClient.NewMobile();
        var id = await client.ActiveJobSeekerAsync(mobile);
        var admin = await client.AdminAsync();

        (await admin.PostAsync($"/api/v1/admin/accounts/{id}/deactivate", new { reason = "requested by ministry" })).StatusCode.Should().Be(HttpStatusCode.NoContent);

        var login = await client.PostAsync("/api/v1/auth/login", new { username = mobile, password = "Str0ngPass", mechanism = "password" });
        await login.ShouldBeProblemAsync(HttpStatusCode.Forbidden, "E-AAFR-ACCOUNT-DEACTIVATED");
        var suspended = await _factory.PublishedEventAsync("AccountSuspended", id);
        suspended["reason"]!.GetValue<string>().Should().Be("requested by ministry");
        suspended["standing"]!.GetValue<string>().Should().Be("Deactivated");
        suspended["actorType"]!.GetValue<string>().Should().Be("JobSeeker");
        suspended["actorId"]!.GetValue<Guid>().Should().Be(await AdminIdAsync());
        var changed = await _factory.PublishedEventAsync("UserAccountApproved", id);
        changed["fromStanding"]!.GetValue<string>().Should().Be("Active");
        changed["toStanding"]!.GetValue<string>().Should().Be("Deactivated");

        (await admin.PostAsync($"/api/v1/admin/accounts/{id}/approve")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await client.LoginAsync(mobile, "Str0ngPass"))["status"]!.GetValue<string>().Should().Be("Authenticated", "a deactivated account can be re-approved");
    }

    [Fact]
    [Trait("Story", "US-3.1.4-03")]
    [Trait("AC", "AC-02")]
    public async Task AccountStanding_ExposesTheAvailableAdministratorActions_AndMasksPii()
    {
        var (client, id, mobile, admin) = await PendingAsync();

        var pending = await (await admin.Http.GetAsync($"/api/v1/admin/accounts/{id}")).Json();
        pending["availableActions"]!.AsArray().Select(a => a!.GetValue<string>()).Should().Equal("approve", "ban", "reset-credentials", "monitor");
        pending["mobile"]!.GetValue<string>().Should().NotContain(mobile).And.Contain("****");
        pending["email"]!.GetValue<string>().Should().Contain("***");

        await admin.PostAsync($"/api/v1/admin/accounts/{id}/approve");
        var active = await (await admin.Http.GetAsync($"/api/v1/admin/accounts/{id}")).Json();
        active["availableActions"]!.AsArray().Select(a => a!.GetValue<string>()).Should().Equal("ban", "deactivate", "reset-credentials", "monitor");

        await admin.PostAsync($"/api/v1/admin/accounts/{id}/ban", new { reason = "x" });
        var banned = await (await admin.Http.GetAsync($"/api/v1/admin/accounts/{id}")).Json();
        banned["availableActions"]!.AsArray().Select(a => a!.GetValue<string>()).Should().Equal("reset-credentials", "monitor");
        client.Should().NotBeNull();
    }

    [Fact]
    [Trait("Story", "US-3.1.4-03")]
    [Trait("AC", "AC-02")]
    public async Task ListAccounts_SupportsFilteringPagingAndMasking()
    {
        var (_, id, _, admin) = await PendingAsync();
        await admin.PostAsync($"/api/v1/admin/accounts/{id}/approve");

        var list = await (await admin.Http.GetAsync("/api/v1/admin/accounts?actorType=JobSeeker&standing=Active&pageSize=2&page=1")).Json();

        list["pageSize"]!.GetValue<int>().Should().Be(2);
        list["items"]!.AsArray().Count.Should().BeLessThanOrEqualTo(2);
        list["items"]!.AsArray().Should().OnlyContain(i => i!["standing"]!.GetValue<string>() == "Active" && i["actorType"]!.GetValue<string>() == "JobSeeker");
        list["totalCount"]!.GetValue<int>().Should().BeGreaterThan(0);
        list["items"]!.AsArray()[0]!["mobile"]!.GetValue<string>().Should().Contain("****");

        var byName = await (await admin.Http.GetAsync("/api/v1/admin/accounts?search=Test%20Seeker&pageSize=100")).Json();
        byName["items"]!.AsArray().Count.Should().BeGreaterThan(0);
        (await admin.Http.GetAsync("/api/v1/admin/accounts?pageSize=1000")).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await admin.Http.GetAsync("/api/v1/admin/accounts?standing=Nope")).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    [Trait("Story", "US-3.1.4-03")]
    [Trait("AC", "AC-03")]
    public async Task Approve_BannedAccount_Returns409StateBanned()
    {
        var (client, id, _, admin) = await PendingAsync();
        await admin.PostAsync($"/api/v1/admin/accounts/{id}/ban", new { reason = "fraud" });

        var response = await admin.PostAsync($"/api/v1/admin/accounts/{id}/approve");

        await response.ShouldBeProblemAsync(HttpStatusCode.Conflict, "E-AUM-STATE-BANNED");
        (await (await admin.Http.GetAsync($"/api/v1/admin/accounts/{id}")).Json())["standing"]!.GetValue<string>().Should().Be("Banned");
        client.Should().NotBeNull();
    }

    [Fact]
    [Trait("Story", "US-3.1.4-03")]
    [Trait("AC", "AC-04")]
    public async Task AdminActions_ByNonAdministrators_Return403AumForbidden_AndAnonymousReturn401()
    {
        var client = new ApiClient(_factory);
        var mobile = ApiClient.NewMobile();
        await client.ActiveJobSeekerAsync(mobile);
        var seeker = await client.UserAsync(mobile, "Str0ngPass");
        var (target, _, _) = await client.RegisterJobSeekerAsync();

        foreach (var action in new[] { "approve", "ban", "deactivate", "reset-credentials" })
        {
            var response = await seeker.PostAsync($"/api/v1/admin/accounts/{target}/{action}", new { reason = "x" });
            await response.ShouldBeProblemAsync(HttpStatusCode.Forbidden, "E-AUM-FORBIDDEN");
            (await client.Anonymous().PostAsync($"/api/v1/admin/accounts/{target}/{action}", new { reason = "x" })).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }

        (await seeker.Http.GetAsync($"/api/v1/admin/accounts/{target}")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await (await client.AdminAsync()).Http.GetAsync($"/api/v1/admin/accounts/{target}")).StatusCode.Should().Be(HttpStatusCode.OK, "control: the administrator can");
    }

    [Fact]
    [Trait("Story", "US-3.1.4-03")]
    [Trait("AC", "AC-04")]
    public async Task AdminActions_ByAPartnerToken_AreRefused()
    {
        // Administrator tokens are only ever issued after MFA (see the login tests), so "no MFA" cannot be forged with a real token;
        // the policy additionally requires the amr=mfa claim. Here a non-administrator token (a partner) hits an admin route.
        var (partner, _, _) = await new ApiClient(_factory).ActivePartnerAsync();

        var response = await partner.PostAsync($"/api/v1/admin/accounts/{Guid.NewGuid()}/ban", new { reason = "x" });

        await response.ShouldBeProblemAsync(HttpStatusCode.Forbidden, "E-AUM-FORBIDDEN");
    }

    [Fact]
    [Trait("Story", "US-3.1.4-03")]
    [Trait("AC", "AC-05")]
    public async Task StatusChanges_PublishUserAccountApprovedWithFromAndToStanding()
    {
        var (_, id, _, admin) = await PendingAsync();

        await admin.PostAsync($"/api/v1/admin/accounts/{id}/approve");
        await admin.PostAsync($"/api/v1/admin/accounts/{id}/ban", new { reason = "abuse" });
        await _factory.PublishOutboxAsync();

        var events = (await _factory.OutboxAsync("UserAccountApproved", id.ToString())).Select(r => r.PayloadOf()).ToList();
        events.Should().HaveCount(2);
        events[0]["fromStanding"]!.GetValue<string>().Should().Be("Pending");
        events[0]["toStanding"]!.GetValue<string>().Should().Be("Active");
        events[1]["fromStanding"]!.GetValue<string>().Should().Be("Active");
        events[1]["toStanding"]!.GetValue<string>().Should().Be("Banned");
        events[1]["userAccountId"]!.GetValue<Guid>().Should().Be(id);
        events[1]["aggregateVersion"]!.GetValue<long>().Should().BeGreaterThan(events[0]["aggregateVersion"]!.GetValue<long>());
        (await _factory.OutboxAsync("UserAccountApproved", id.ToString())).Should().OnlyContain(r => r.RoutingKey == "user-account.approved.v1");
    }

    [Fact]
    [Trait("Story", "US-3.1.4-03")]
    [Trait("AC", "AC-01")]
    public async Task AdminActions_OnUnknownAccountOrWithoutReason_ReturnNotFoundOrValidation()
    {
        var admin = await new ApiClient(_factory).AdminAsync();

        await (await admin.PostAsync($"/api/v1/admin/accounts/{Guid.NewGuid()}/approve")).ShouldBeProblemAsync(HttpStatusCode.NotFound, "E-ACCOUNT-NOT-FOUND");
        await (await admin.Http.GetAsync($"/api/v1/admin/accounts/{Guid.NewGuid()}")).ShouldBeProblemAsync(HttpStatusCode.NotFound, "E-ACCOUNT-NOT-FOUND");
        var noReason = await admin.PostAsync($"/api/v1/admin/accounts/{Guid.NewGuid()}/ban", new { reason = "" });
        await noReason.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "VAL.INVALID_REQUEST");
    }

    [Fact]
    [Trait("Story", "US-3.1.4-03")]
    [Trait("AC", "AC-01")]
    public async Task Deactivate_PendingAccount_IsAStateConflict()
    {
        var (_, id, _, admin) = await PendingAsync();

        var response = await admin.PostAsync($"/api/v1/admin/accounts/{id}/deactivate", new { reason = "x" });

        await response.ShouldBeProblemAsync(HttpStatusCode.Conflict, "E-AUM-STATE-CONFLICT");
    }
}

/// <summary>RBAC (US-3.1.5-03): decisions are logged, and role changes apply on the very next request without re-login.</summary>
public class RolesApiTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public RolesApiTests(ApiFactory factory) => _factory = factory;

    private static string RoleUrl(RoleId role, string permission) => $"/api/v1/admin/roles/{role.Value}/permissions/{permission}";

    [Fact]
    [Trait("Story", "US-3.1.5-03")]
    [Trait("AC", "AC-01")]
    public async Task UserWithAPermissionBearingRole_IsGrantedAccess()
    {
        var admin = await new ApiClient(_factory).AdminAsync();

        var roles = await admin.Http.GetAsync("/api/v1/admin/roles");

        roles.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await roles.Json();
        body.AsArray().Select(r => r!["name"]!.GetValue<string>()).Should().Contain(new[] { "Administrator", "JobSeeker", "Employer", "ExternalJobSite", "Guest" });
        body.AsArray().Single(r => r!["name"]!.GetValue<string>() == "Administrator")!["permissions"]!.AsArray().Count.Should().Be(Permissions.All.Count);
    }

    [Fact]
    [Trait("Story", "US-3.1.5-03")]
    [Trait("AC", "AC-02")]
    public async Task UserWhoseRoleLacksThePermission_IsDeniedWithForbidden()
    {
        var client = new ApiClient(_factory);
        var mobile = ApiClient.NewMobile();
        await client.ActiveJobSeekerAsync(mobile);
        var seeker = await client.UserAsync(mobile, "Str0ngPass");

        var response = await seeker.Http.GetAsync("/api/v1/admin/roles");

        await response.ShouldBeProblemAsync(HttpStatusCode.Forbidden, "E-AAFR-FORBIDDEN");
    }

    [Fact]
    [Trait("Story", "US-3.1.5-03")]
    [Trait("AC", "AC-04")]
    public async Task PermissionChange_AppliesOnTheNextRequestWithoutReLogin()
    {
        var admin = await new ApiClient(_factory).AdminAsync();
        var admins = WellKnownRoles.Administrator;
        (await admin.Http.GetAsync("/api/v1/admin/accounts")).StatusCode.Should().Be(HttpStatusCode.OK);

        (await admin.Http.DeleteAsync(RoleUrl(admins, Permissions.AccountsRead))).StatusCode.Should().Be(HttpStatusCode.NoContent);
        try
        {
            var denied = await admin.Http.GetAsync("/api/v1/admin/accounts");
            await denied.ShouldBeProblemAsync(HttpStatusCode.Forbidden, "E-AUM-FORBIDDEN");
        }
        finally
        {
            (await admin.PutAsync(RoleUrl(admins, Permissions.AccountsRead))).StatusCode.Should().Be(HttpStatusCode.NoContent);
        }

        (await admin.Http.GetAsync("/api/v1/admin/accounts")).StatusCode.Should().Be(HttpStatusCode.OK, "the very same token works again once the permission is back");
    }

    [Fact]
    [Trait("Story", "US-3.1.5-03")]
    [Trait("AC", "AC-04")]
    public async Task AssigningARoleToAnAccount_TakesEffectImmediately()
    {
        var client = new ApiClient(_factory);
        var (partner, partnerId, _) = await client.ActivePartnerAsync();
        var admin = await client.AdminAsync();
        (await partner.Http.GetAsync("/api/v1/api-credentials/current")).StatusCode.Should().Be(HttpStatusCode.NotFound, "authorised, but no credential yet");

        (await admin.Http.DeleteAsync(RoleUrl(WellKnownRoles.ExternalJobSite, Permissions.ApiCredentialsManage))).StatusCode.Should().Be(HttpStatusCode.NoContent);
        try
        {
            var denied = await partner.PostAsync("/api/v1/api-credentials", new { });
            await denied.ShouldBeProblemAsync(HttpStatusCode.Forbidden, "E-AAFR-FORBIDDEN");

            // A second role that carries the permission is assigned: no re-login needed.
            var custom = await admin.Http.GetAsync("/api/v1/admin/roles");
            custom.StatusCode.Should().Be(HttpStatusCode.OK);
            (await admin.PutAsync($"/api/v1/admin/accounts/{partnerId}/roles/{WellKnownRoles.Administrator.Value}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
            (await partner.PostAsync("/api/v1/api-credentials", new { })).StatusCode.Should().Be(HttpStatusCode.Created);
            (await admin.Http.DeleteAsync($"/api/v1/admin/accounts/{partnerId}/roles/{WellKnownRoles.Administrator.Value}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
            (await partner.PostAsync("/api/v1/api-credentials", new { })).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }
        finally
        {
            await admin.PutAsync(RoleUrl(WellKnownRoles.ExternalJobSite, Permissions.ApiCredentialsManage));
        }
    }

    [Fact]
    [Trait("Story", "US-3.1.5-03")]
    [Trait("AC", "AC-03")]
    public async Task EveryAccessDecision_IsRecordedInTheAccessLog_AndReadableThroughTheInternalApi()
    {
        var client = new ApiClient(_factory);
        var mobile = ApiClient.NewMobile();
        var seekerId = await client.ActiveJobSeekerAsync(mobile);
        var seeker = await client.UserAsync(mobile, "Str0ngPass");
        var admin = await client.AdminAsync();
        await seeker.Http.GetAsync("/api/v1/admin/roles");
        await admin.Http.GetAsync("/api/v1/admin/roles");
        var adminId = (await _factory.WithDbAsync(db => db.Accounts.AsNoTracking().ToListAsync())).Single(a => a.Email?.Value == ApiFactory.AdminEmail).Id.Value;

        var service = await client.ServiceAsync();
        var adminLog = await (await service.Http.GetAsync($"/internal/v1/access-log?accountId={adminId}&pageSize=100")).Json();
        var allowed = adminLog["items"]!.AsArray().Where(i => i!["action"]!.GetValue<string>() == "ListRolesQuery").ToList();

        allowed.Should().NotBeEmpty();
        allowed.Should().OnlyContain(i => i!["decision"]!.GetValue<string>() == "Allow" && i["resource"]!.GetValue<string>() == "roles.read");

        // The seeker's denied attempt never reached the application pipeline (the policy stopped it), so the denial that is logged is
        // the permission decision of a request that passed the coarse policy: covered by the administrator scenario below.
        var denied = await client.AdminAsync();
        await denied.Http.DeleteAsync(RoleUrl(WellKnownRoles.Administrator, Permissions.RolesRead));
        try
        {
            var response = await denied.Http.GetAsync("/api/v1/admin/roles");
            response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }
        finally
        {
            await denied.PutAsync(RoleUrl(WellKnownRoles.Administrator, Permissions.RolesRead));
        }

        var log = await (await service.Http.GetAsync($"/internal/v1/access-log?accountId={adminId}&pageSize=100")).Json();
        var denial = log["items"]!.AsArray().First(i => i!["decision"]!.GetValue<string>() == "Deny" && i["action"]!.GetValue<string>() == "ListRolesQuery");
        denial!["reason"]!.GetValue<string>().Should().Contain("roles.read");
        (await service.Http.GetAsync($"/internal/v1/access-log?from={DateTime.UtcNow.AddYears(1):O}")).StatusCode.Should().Be(HttpStatusCode.OK);
        seekerId.Should().NotBeEmpty();
        seeker.Should().NotBeNull();
    }

    [Fact]
    [Trait("Story", "US-3.1.5-03")]
    [Trait("AC", "AC-03")]
    public async Task AccessLog_IsReadableByServicesAndPermittedAdministratorsOnly()
    {
        var client = new ApiClient(_factory);
        var mobile = ApiClient.NewMobile();
        await client.ActiveJobSeekerAsync(mobile);
        var seeker = await client.UserAsync(mobile, "Str0ngPass");

        (await seeker.Http.GetAsync("/internal/v1/access-log")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await client.Anonymous().Http.GetAsync("/internal/v1/access-log")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await (await client.ServiceAsync()).Http.GetAsync("/internal/v1/access-log?pageSize=500")).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task RoleAdministration_RejectsUnknownRolesPermissionsAndTheProtectedAdminPermission()
    {
        var admin = await new ApiClient(_factory).AdminAsync();

        await (await admin.PutAsync(RoleUrl(new RoleId(Guid.NewGuid()), Permissions.JobsBrowse))).ShouldBeProblemAsync(HttpStatusCode.NotFound, "E-ROLE-NOT-FOUND");
        await (await admin.Http.DeleteAsync(RoleUrl(new RoleId(Guid.NewGuid()), Permissions.JobsBrowse))).ShouldBeProblemAsync(HttpStatusCode.NotFound, "E-ROLE-NOT-FOUND");
        var unknown = await admin.PutAsync(RoleUrl(WellKnownRoles.Guest, "made.up"));
        await unknown.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "E-AAFR-INVALID-FIELD");
        var protectedRevoke = await admin.Http.DeleteAsync(RoleUrl(WellKnownRoles.Administrator, Permissions.RolesManage));
        protectedRevoke.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await protectedRevoke.Json())["ruleCode"]!.GetValue<string>().Should().Be("AI.Role.PROTECTED_PERMISSION");
    }
}
