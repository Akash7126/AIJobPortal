using System.Net;
using JobPlatform.SharedKernel.Common.Enums;
using JobPlatform.SharedKernel.IntegrationEvents.AccountIdentity;
using JobPlatform.SharedKernel.IntegrationEvents.Audit;
using JobPlatform.SharedKernel.IntegrationEvents.CandidateSourcing;
using JobPlatform.SharedKernel.IntegrationEvents.EmployerOnboarding;
using JobPlatform.SharedKernel.IntegrationEvents.ExternalIntegration;
using JobPlatform.SharedKernel.IntegrationEvents.GovernmentIntegration;
using JobPlatform.SharedKernel.IntegrationEvents.JobPosting;
using JobPlatform.SharedKernel.IntegrationEvents.JobSeekerProfile;
using JobPlatform.SharedKernel.IntegrationEvents.Notification;
using JobPlatform.SharedKernel.IntegrationEvents.PlatformAdministration;
using JobPlatform.AuditLogging.Infrastructure.Persistence;
using JobPlatform.TestSupport;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace JobPlatform.AuditLogging.Api.IntegrationTests;

public class ApiTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _f;
    private static readonly Guid Platform = Guid.NewGuid();

    public ApiTests(ApiFactory factory) => _f = factory;

    private DateTime Now => _f.Clock.GetUtcNow().UtcDateTime;

    private JobDataImportedIntegrationEvent Imported(Guid partner, string job, bool update = false) =>
        new(Guid.NewGuid(), Now, Guid.NewGuid(), null, Guid.NewGuid(), Platform, partner, job, "s-" + job, "Engineer", "Sum", new[] { "C#" }, "FullTime", "OnSite", null, "Gaza", null,
            "Visible", update, 1);

    private AuditRecordIntegrationEvent Record(Guid partner, string category, string outcome, string code, string? job = null, string? retry = null) =>
        new(Guid.NewGuid(), Now, Guid.NewGuid(), null, "external-integration", category, partner, "Request", "r-" + Guid.NewGuid().ToString("N")[..6], $"Partner:{partner}", "Post",
            outcome, code, job is null ? null : new Dictionary<string, string> { ["platformJobId"] = job, ["sourcePlatformId"] = Platform.ToString(), ["retry"] = retry ?? "false" });

    // ---------------------------------------------------------------- authentication / authorisation matrix

    public static IEnumerable<object[]> AllGetRoutes() => new[]
    {
        "/api/v1/employers/me/dashboard", "/api/v1/partners/me/api-responses", "/api/v1/partners/me/submissions", "/api/v1/partners/me/sync-dashboard",
        "/api/v1/partners/me/sync-errors", "/api/v1/partners/me/integration-status", "/api/v1/partners/me/usage-statistics?from=2026-01-01&to=2026-01-31",
        "/api/v1/admin/audit/jobs/PJ-1", "/api/v1/admin/audit/admin-actions", "/api/v1/admin/audit/access", "/api/v1/admin/audit/government-exchanges",
        "/api/v1/admin/audit/emails", "/api/v1/admin/audit/sms", "/api/v1/users/me/notifications/history", $"/api/v1/jobs/{Guid.NewGuid()}/status-history",
        $"/api/v1/employers/me/candidates/{Guid.NewGuid()}/insight?jobPostingId={Guid.NewGuid()}", $"/api/v1/admin/reports/exports/{Guid.NewGuid()}"
    }.Select(r => new object[] { r });

    [Theory]
    [MemberData(nameof(AllGetRoutes))]
    public async Task EveryEndpoint_WithoutAToken_Is401ProblemJson(string route)
    {
        var response = await _f.ClientFor(null).GetAsync(route);

        await response.ShouldBeProblemAsync(HttpStatusCode.Unauthorized, "E-AAFR-UNAUTHORIZED");
    }

    [Theory]
    [InlineData("/api/v1/partners/me/api-responses", "Employer", "E-TPJPRI-FORBIDDEN")]
    [InlineData("/api/v1/partners/me/submissions", "JobSeeker", "E-TPJPRI-FORBIDDEN")]
    [InlineData("/api/v1/partners/me/sync-dashboard", "Administrator", "E-TPJPRI-FORBIDDEN")]
    [InlineData("/api/v1/partners/me/sync-errors", "Employer", "E-TPJPRI-FORBIDDEN")]
    [InlineData("/api/v1/partners/me/integration-status", "Employer", "E-TPJPRI-FORBIDDEN")]
    [InlineData("/api/v1/partners/me/usage-statistics?from=2026-01-01&to=2026-01-31", "Employer", "E-TPJPRI-FORBIDDEN")]
    [InlineData("/api/v1/admin/audit/admin-actions", "Employer", "E-AUM-FORBIDDEN")]
    [InlineData("/api/v1/admin/audit/access", "JobSeeker", "E-AAFR-FORBIDDEN")]
    [InlineData("/api/v1/admin/audit/government-exchanges", "ExternalJobSite", "E-GDI-FORBIDDEN")]
    [InlineData("/api/v1/admin/audit/emails", "Employer", "E-EMAILN-FORBIDDEN")]
    [InlineData("/api/v1/admin/audit/sms", "JobSeeker", "E-SMSN-FORBIDDEN")]
    [InlineData("/api/v1/admin/audit/jobs/PJ-1", "ExternalJobSite", "E-AUM-FORBIDDEN")]
    [InlineData("/api/v1/employers/me/dashboard", "JobSeeker", "E-AUDIT-EMPLOYER-FORBIDDEN")]
    public async Task WrongActorType_Is403WithTheStoryErrorCode(string route, string actor, string code)
    {
        var token = Enum.Parse<ActorType>(actor) switch
        {
            ActorType.Administrator => TestTokens.Admin(),
            var other => TestTokens.Issue(other, scope: null)
        };

        var response = await _f.ClientFor(token).GetAsync(route);

        await response.ShouldBeProblemAsync(HttpStatusCode.Forbidden, code);
    }

    [Fact]
    public async Task AdministratorWithoutMfa_IsRefused()
    {
        var token = TestTokens.Issue(ActorType.Administrator, mfa: false);

        var response = await _f.ClientFor(token).GetAsync("/api/v1/admin/audit/access");

        await response.ShouldBeProblemAsync(HttpStatusCode.Forbidden, "E-AAFR-FORBIDDEN");
    }

    [Fact]
    public async Task ExpiredOrTamperedToken_Is401()
    {
        var expired = TestTokens.Issue(ActorType.Administrator, mfa: true, lifetime: TimeSpan.FromMinutes(1), nowUtc: DateTime.UtcNow.AddHours(-2));
        (await _f.ClientFor(expired).GetAsync("/api/v1/admin/audit/access")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var tampered = TestTokens.Admin()[..^4] + "AAAA";
        (await _f.ClientFor(tampered).GetAsync("/api/v1/admin/audit/access")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ---------------------------------------------------------------- partner logs and dashboards

    [Fact]
    [Trait("Story", "US-3.1.3-07")]
    [Trait("AC", "AC-01")]
    public async Task Partner_SeesOwnSubmissions_AndNeverAnotherPartners()
    {
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        await _f.IngestAsync(Imported(a, "A-1"), Imported(a, "A-2"), Imported(b, "B-1"));

        var mine = await (await _f.ClientFor(TestTokens.Partner(a)).GetAsync("/api/v1/partners/me/submissions")).Json();
        mine["totalCount"]!.GetValue<int>().Should().Be(2);
        mine["items"]!.AsArray().Select(i => i!["subjectId"]!.GetValue<string>()).Should().BeEquivalentTo("A-1", "A-2");

        var other = await (await _f.ClientFor(TestTokens.Partner(b)).GetAsync("/api/v1/partners/me/submissions")).Json();
        other["totalCount"]!.GetValue<int>().Should().Be(1);
    }

    [Fact]
    [Trait("Story", "US-3.1.3-06")]
    [Trait("AC", "AC-01")]
    public async Task Partner_ApiResponseLog_FiltersByOutcomeAndShowsCodes()
    {
        var p = Guid.NewGuid();
        await _f.IngestAsync(Record(p, "ApiCall", "Success", "200"), Record(p, "ApiCall", "Failure", "E-TPJPRI-INVALID-FIELD"), Record(p, "ApiCall", "Duplicate", "E-TPJPRI-DUPLICATE"));

        var failures = await (await _f.ClientFor(TestTokens.Partner(p)).GetAsync("/api/v1/partners/me/api-responses?outcome=failure")).Json();

        failures["items"]!.AsArray().Should().ContainSingle().Which!["code"]!.GetValue<string>().Should().Be("E-TPJPRI-INVALID-FIELD");
        var all = await (await _f.ClientFor(TestTokens.Partner(p)).GetAsync("/api/v1/partners/me/api-responses")).Json();
        all["totalCount"]!.GetValue<int>().Should().Be(3);
    }

    [Fact]
    public async Task ListQueries_WithBadFilters_Are400WithFieldCodes()
    {
        var client = _f.ClientFor(TestTokens.Partner(Guid.NewGuid()));

        var outcome = await client.GetAsync("/api/v1/partners/me/api-responses?outcome=bogus");
        await outcome.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "VAL.INVALID_REQUEST");
        (await outcome.Json())["errors"]!["outcome"]![0]!.GetValue<string>().Should().Be("VAL.Outcome.Invalid");

        var size = await client.GetAsync("/api/v1/partners/me/submissions?pageSize=101");
        (await size.Json())["errors"]!["pageSize"]![0]!.GetValue<string>().Should().Be("VAL.PageSize.OutOfRange");
    }

    [Fact]
    [Trait("Story", "US-3.1.3-10")]
    [Trait("AC", "AC-01")]
    public async Task SyncDashboard_ShowsStatusPerJob_AndRetryGoesFailedPendingSynced()
    {
        var p = Guid.NewGuid();
        var client = _f.ClientFor(TestTokens.Partner(p));
        await _f.IngestAsync(Imported(p, $"OK-{p:N}"), Record(p, "SyncError", "Failure", "E-SYNC-TIMEOUT", $"BAD-{p:N}"));

        var dashboard = await (await client.GetAsync("/api/v1/partners/me/sync-dashboard")).Json();
        (dashboard["synced"]!.GetValue<int>(), dashboard["failed"]!.GetValue<int>()).Should().Be((1, 1));
        dashboard["jobs"]!["items"]!.AsArray().Single(j => j!["platformJobId"]!.GetValue<string>() == $"BAD-{p:N}")!["reasonCode"]!.GetValue<string>().Should().Be("E-SYNC-TIMEOUT");

        await _f.IngestAsync(Record(p, "SyncError", "Success", "OK", $"BAD-{p:N}", "true"));

        var after = await (await client.GetAsync("/api/v1/partners/me/sync-dashboard")).Json();
        (after["synced"]!.GetValue<int>(), after["failed"]!.GetValue<int>()).Should().Be((2, 0));

        var errors = await (await client.GetAsync("/api/v1/partners/me/sync-errors")).Json();
        errors["totalCount"]!.GetValue<int>().Should().Be(2);
    }

    [Fact]
    [Trait("Story", "US-3.4.1-06")]
    [Trait("AC", "AC-01")]
    public async Task IntegrationStatus_ReportsHealthAndSubmissions()
    {
        var p = Guid.NewGuid();
        await _f.IngestAsync(Imported(p, $"OK-{p:N}"), Record(p, "SyncError", "Failure", "E-X", $"BAD-{p:N}"));

        var status = await (await _f.ClientFor(TestTokens.Partner(p)).GetAsync("/api/v1/partners/me/integration-status")).Json();

        status["health"]!.GetValue<string>().Should().Be("Degraded");
        status["failed"]!.GetValue<int>().Should().Be(1);
        status["submittedLast30Days"]!.GetValue<int>().Should().Be(1);
        var fresh = await (await _f.ClientFor(TestTokens.Partner(Guid.NewGuid())).GetAsync("/api/v1/partners/me/integration-status")).Json();
        fresh["health"]!.GetValue<string>().Should().Be("Idle");
    }

    [Fact]
    [Trait("Story", "US-3.1.3-11")]
    [Trait("AC", "AC-01")]
    public async Task UsageStatistics_CountsSubmissionsInTheRange()
    {
        var p = Guid.NewGuid();
        await _f.IngestAsync(Imported(p, "U-1"), Imported(p, "U-2"), Imported(p, "U-1", update: true));
        var day = DateOnly.FromDateTime(Now);

        var usage = await (await _f.ClientFor(TestTokens.Partner(p)).GetAsync($"/api/v1/partners/me/usage-statistics?from={day.AddDays(-2):yyyy-MM-dd}&to={day:yyyy-MM-dd}")).Json();

        usage["submitted"]!.GetValue<int>().Should().Be(2, "an update of a known job is not a new submission");
        usage["days"]!.AsArray().Should().ContainSingle();
    }

    [Theory]
    [Trait("Story", "US-3.1.3-11")]
    [Trait("AC", "AC-02")]
    [InlineData("2026-03-10", "2026-03-01")]
    [InlineData("2024-01-01", "2026-03-01")]
    public async Task UsageStatistics_InvalidRange_Is400InvalidField(string from, string to)
    {
        var response = await _f.ClientFor(TestTokens.Partner(Guid.NewGuid())).GetAsync($"/api/v1/partners/me/usage-statistics?from={from}&to={to}");

        await response.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "E-TPJPRI-INVALID-FIELD");
    }

    // ---------------------------------------------------------------- administrator logs

    [Fact]
    [Trait("Story", "US-3.1.5-05")]
    [Trait("AC", "AC-01")]
    public async Task Admin_AccessLog_ListsAccountEvents_AndRedeliveryIsRecordedOnce()
    {
        var account = Guid.NewGuid();
        var created = new AccountCreatedIntegrationEvent(Guid.NewGuid(), Now, Guid.NewGuid(), null, account, account, ActorType.JobSeeker, 1);
        await _f.IngestAsync(created);
        await _f.IngestAsync(created with { }); // same MessageId delivered again

        var log = await (await _f.ClientFor(TestTokens.Admin()).GetAsync("/api/v1/admin/audit/access")).Json();

        log["items"]!.AsArray().Count(i => i!["subjectId"]!.GetValue<string>() == account.ToString()).Should().Be(1);
    }

    [Fact]
    [Trait("Story", "US-3.1.4-05")]
    [Trait("AC", "AC-01")]
    public async Task Admin_AdminActionsAndGovernmentTrail_ListTheirEvents()
    {
        var admin = Guid.NewGuid();
        var employer = Guid.NewGuid();
        await _f.IngestAsync(
            new PlatformTaxonomyUpdatedIntegrationEvent(Guid.NewGuid(), Now, Guid.NewGuid(), null, Guid.NewGuid(), "Draft", "Active", admin, "Skills", 1, 2, new[] { "S1" }, 1),
            new EmployerVerificationApprovedIntegrationEvent(Guid.NewGuid(), Now, Guid.NewGuid(), null, Guid.NewGuid(), employer, admin, "ManualMoL", 1),
            new GovernmentVerificationDataImportedIntegrationEvent(Guid.NewGuid(), Now, Guid.NewGuid(), null, Guid.NewGuid(), "Employer", employer, "Verified", 1),
            new ProfileCreatedIntegrationEvent(Guid.NewGuid(), Now, Guid.NewGuid(), null, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), null, 1),
            new PlatformEntityRecordCreatedIntegrationEvent(Guid.NewGuid(), Now, Guid.NewGuid(), null, Guid.NewGuid(), admin, "Region", 1),
            new JobOfferingSuspendedIntegrationEvent(Guid.NewGuid(), Now, Guid.NewGuid(), null, Guid.NewGuid(), Guid.NewGuid(), admin, "policy", 1),
            new UserAccountApprovedIntegrationEvent(Guid.NewGuid(), Now, Guid.NewGuid(), null, Guid.NewGuid(), admin, "Pending", "Active", 1),
            new ApiCredentialCreatedIntegrationEvent(Guid.NewGuid(), Now, Guid.NewGuid(), null, Guid.NewGuid(), Guid.NewGuid(), admin, Now.AddYears(1), 1));
        var client = _f.ClientFor(TestTokens.Admin());

        var actions = (await (await client.GetAsync("/api/v1/admin/audit/admin-actions")).Json())["items"]!.AsArray().Select(i => i!["action"]!.GetValue<string>()).ToList();
        actions.Should().Contain(new[] { "PlatformTaxonomyUpdated", "PlatformEntityRecordCreated", "JobOfferingSuspended", "UserAccountStandingChanged", "ApiCredentialCreated" });

        var gov = (await (await client.GetAsync("/api/v1/admin/audit/government-exchanges")).Json())["items"]!.AsArray().Select(i => i!["action"]!.GetValue<string>()).ToList();
        gov.Should().Contain(new[] { "EmployerVerificationApproved", "GovernmentQueryCompleted" });
    }

    [Fact]
    [Trait("Story", "US-3.1.3-14")]
    [Trait("AC", "AC-01")]
    public async Task Admin_JobAuditTrail_ShowsWhichPartnerCreatedAndModifiedTheJob()
    {
        var p = Guid.NewGuid();
        await _f.IngestAsync(Imported(p, "TRAIL-1"), Imported(p, "TRAIL-1", update: true),
            new JobPostAttributionUpdatedIntegrationEvent(Guid.NewGuid(), Now, Guid.NewGuid(), null, Guid.NewGuid(), "Active", "Closed", p, "TRAIL-1", null, null, 3));

        var trail = await (await _f.ClientFor(TestTokens.Admin()).GetAsync("/api/v1/admin/audit/jobs/TRAIL-1")).Json();

        trail["totalCount"]!.GetValue<int>().Should().Be(3);
        trail["items"]!.AsArray().Should().OnlyContain(i => i!["actorId"]!.GetValue<Guid>() == p);
    }

    [Fact]
    [Trait("Story", "US-3.1.5-05")]
    [Trait("AC", "AC-02")]
    public async Task AuditRecordStream_WithPersonalData_IsStoredRedacted()
    {
        await _f.IngestAsync(new AuditRecordIntegrationEvent(Guid.NewGuid(), Now, Guid.NewGuid(), null, "account-identity", "Access", null, "Login", "unknown", null, "LoginFailed",
            "Denied", "E-AAFR-INVALID-CREDENTIALS", new Dictionary<string, string> { ["email"] = "someone@example.com", ["ip"] = "10.0.0.1" }));

        var log = await (await _f.ClientFor(TestTokens.Admin()).GetAsync("/api/v1/admin/audit/access?outcome=denied")).Json();

        var entry = log["items"]!.AsArray().First(i => i!["action"]!.GetValue<string>() == "LoginFailed")!;
        entry["details"]!.AsObject().ContainsKey("email").Should().BeFalse();
        entry["details"]!["redacted"]!.GetValue<string>().Should().Be("1");
        entry["outcome"]!.GetValue<string>().Should().Be("Denied");
    }

    // ---------------------------------------------------------------- notification logs

    [Fact]
    [Trait("Story", "US-3.6.1-04")]
    [Trait("AC", "AC-01")]
    public async Task EmailAndSmsLogs_AreAdminOnly_WithMaskedRecipients_AndStatusUpdates()
    {
        var user = Guid.NewGuid();
        var email = Guid.NewGuid();
        var sms = Guid.NewGuid();
        await _f.IngestAsync(
            new NotificationSentIntegrationEvent(Guid.NewGuid(), Now, Guid.NewGuid(), null, email, "Email", "Welcome", user, "s***@example.com", "Welcome!", "Sent", 1),
            new NotificationSentIntegrationEvent(Guid.NewGuid(), Now, Guid.NewGuid(), null, sms, "Sms", "Otp", user, "+970****4567", null, "Sent", 1),
            new NotificationStatusUpdatedIntegrationEvent(Guid.NewGuid(), Now.AddMinutes(1), Guid.NewGuid(), null, sms, "Sent", "Delivered", user, 2));
        var admin = _f.ClientFor(TestTokens.Admin());

        var emails = (await (await admin.GetAsync("/api/v1/admin/audit/emails")).Json())["items"]!.AsArray();
        emails.Should().Contain(i => i!["notificationId"]!.GetValue<Guid>() == email && i["maskedRecipient"]!.GetValue<string>() == "s***@example.com");

        var smsLog = (await (await admin.GetAsync("/api/v1/admin/audit/sms")).Json())["items"]!.AsArray();
        smsLog.Single(i => i!["notificationId"]!.GetValue<Guid>() == sms)!["status"]!.GetValue<string>().Should().Be("Delivered");
    }

    [Fact]
    [Trait("Story", "US-3.6.2-04")]
    [Trait("AC", "AC-01")]
    public async Task NotificationHistory_IsOwnOnly_NewestFirst()
    {
        var me = Guid.NewGuid();
        var other = Guid.NewGuid();
        await _f.IngestAsync(
            new NotificationSentIntegrationEvent(Guid.NewGuid(), Now, Guid.NewGuid(), null, Guid.NewGuid(), "InApp", "News", me, "user", "old", "Unread", 1),
            new NotificationSentIntegrationEvent(Guid.NewGuid(), Now.AddMinutes(5), Guid.NewGuid(), null, Guid.NewGuid(), "InApp", "Match", me, "user", "new", "Unread", 1),
            new NotificationSentIntegrationEvent(Guid.NewGuid(), Now, Guid.NewGuid(), null, Guid.NewGuid(), "InApp", "News", other, "user", "theirs", "Unread", 1));

        var history = await (await _f.ClientFor(TestTokens.JobSeeker(me)).GetAsync("/api/v1/users/me/notifications/history")).Json();

        history["totalCount"]!.GetValue<int>().Should().Be(2);
        history["items"]!.AsArray().Select(i => i!["subject"]!.GetValue<string>()).Should().Equal("new", "old");
        (await _f.ClientFor(TestTokens.Employer(me)).GetAsync("/api/v1/users/me/notifications/history")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // ---------------------------------------------------------------- employer views

    [Fact]
    [Trait("Story", "US-3.1.2-07")]
    [Trait("AC", "AC-02")]
    public async Task EmployerDashboard_ShowsPostingsShortlistsAndRegistration_OwnerOnly()
    {
        var employer = Guid.NewGuid();
        var job = Guid.NewGuid();
        await _f.IngestAsync(
            new EmployerRegistrationApprovedIntegrationEvent(Guid.NewGuid(), Now, Guid.NewGuid(), null, Guid.NewGuid(), Guid.NewGuid(), employer, 1),
            new JobPostingCreatedIntegrationEvent(Guid.NewGuid(), Now, Guid.NewGuid(), null, job, employer, employer, "Draft", "Engineer", "IT", new[] { "C#" }, "Public", "Employer", null, null, null, 1),
            new JobPostingStatusUpdatedIntegrationEvent(Guid.NewGuid(), Now.AddMinutes(1), Guid.NewGuid(), null, job, job, employer, "Draft", "Active", employer, null, 2),
            new TalentPoolEntryCreatedIntegrationEvent(Guid.NewGuid(), Now, Guid.NewGuid(), null, Guid.NewGuid(), job, employer, employer, 1));

        var mine = await (await _f.ClientFor(TestTokens.Employer(employer)).GetAsync("/api/v1/employers/me/dashboard")).Json();
        (mine["registrationApproved"]!.GetValue<bool>(), mine["postings"]!.GetValue<int>(), mine["activePostings"]!.GetValue<int>(), mine["shortlists"]!.GetValue<int>()).Should().Be((true, 1, 1, 1));

        var another = await (await _f.ClientFor(TestTokens.Employer(Guid.NewGuid())).GetAsync("/api/v1/employers/me/dashboard")).Json();
        another["postings"]!.GetValue<int>().Should().Be(0);
    }

    [Fact]
    [Trait("Story", "US-3.2.4-02")]
    [Trait("AC", "AC-01")]
    public async Task JobStatusHistory_OwnerSeesRows_OthersGetJstForbidden()
    {
        var employer = Guid.NewGuid();
        var job = Guid.NewGuid();
        await _f.IngestAsync(
            new JobPostingCreatedIntegrationEvent(Guid.NewGuid(), Now, Guid.NewGuid(), null, job, employer, employer, "Draft", "T", "IT", Array.Empty<string>(), "Public", "Employer", null, null, null, 1),
            new JobPostingStatusUpdatedIntegrationEvent(Guid.NewGuid(), Now.AddMinutes(1), Guid.NewGuid(), null, job, job, employer, "Draft", "Active", employer, null, 2),
            new JobPostingStatusUpdatedIntegrationEvent(Guid.NewGuid(), Now.AddMinutes(2), Guid.NewGuid(), null, job, job, employer, "Active", "Closed", employer, "filled", 3));

        var rows = await (await _f.ClientFor(TestTokens.Employer(employer)).GetAsync($"/api/v1/jobs/{job}/status-history")).Json();
        rows.AsArray().Select(r => r!["toStatus"]!.GetValue<string>()).Should().Equal("Draft", "Active", "Closed");

        var stranger = await _f.ClientFor(TestTokens.Employer(Guid.NewGuid())).GetAsync($"/api/v1/jobs/{job}/status-history");
        await stranger.ShouldBeProblemAsync(HttpStatusCode.Forbidden, "E-JST-FORBIDDEN");

        var unknown = await _f.ClientFor(TestTokens.Employer(employer)).GetAsync($"/api/v1/jobs/{Guid.NewGuid()}/status-history");
        unknown.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    [Trait("Story", "US-3.3.3-06")]
    [Trait("AC", "AC-02")]
    public async Task CandidateInsight_WithheldFieldsAreUnavailable_OwnerOnly_UnknownIs404()
    {
        var employer = Guid.NewGuid();
        var job = Guid.NewGuid();
        var candidate = Guid.NewGuid();
        await _f.IngestAsync(new CandidateInsightComputedIntegrationEvent(Guid.NewGuid(), Now, Guid.NewGuid(), null, Guid.NewGuid(), job, employer, employer, candidate,
            "Immediate", 900m, 91.5m, new[] { "expectedSalary" }, 1));
        var url = $"/api/v1/employers/me/candidates/{candidate}/insight?jobPostingId={job}";

        var insight = await (await _f.ClientFor(TestTokens.Employer(employer)).GetAsync(url)).Json();
        insight["availability"]!.GetValue<string>().Should().Be("Immediate");
        insight["expectedSalary"]!.GetValue<string>().Should().Be("unavailable");
        insight["fit"]!.GetValue<string>().Should().Be("91.5");

        await (await _f.ClientFor(TestTokens.Employer(Guid.NewGuid())).GetAsync(url)).ShouldBeProblemAsync(HttpStatusCode.Forbidden, "E-AUDIT-INSIGHT-FORBIDDEN");
        await (await _f.ClientFor(TestTokens.Employer(employer)).GetAsync($"/api/v1/employers/me/candidates/{Guid.NewGuid()}/insight?jobPostingId={job}"))
            .ShouldBeProblemAsync(HttpStatusCode.NotFound, "E-AUDIT-INSIGHT-NOT-FOUND");
    }

    // ---------------------------------------------------------------- report export

    [Fact]
    [Trait("Story", "US-3.1.4-10")]
    [Trait("AC", "AC-01")]
    public async Task Export_IsAcceptedThenReused_ThenGeneratedToReady()
    {
        var admin = _f.ClientFor(TestTokens.Admin(Guid.NewGuid()));
        var body = new { reportType = "PostingsByRegion", format = "Csv", parameters = new Dictionary<string, string> { ["year"] = "2026" } };

        var first = await admin.PostJsonAsync("/api/v1/admin/reports/exports", body);
        first.StatusCode.Should().Be(HttpStatusCode.Accepted);
        var job = await first.Json();
        first.Headers.Location!.ToString().Should().EndWith(job["id"]!.GetValue<string>());
        job["status"]!.GetValue<string>().Should().Be("Queued");

        var second = await admin.PostJsonAsync("/api/v1/admin/reports/exports", body);
        second.StatusCode.Should().Be(HttpStatusCode.OK);
        (await second.Json())["id"]!.GetValue<string>().Should().Be(job["id"]!.GetValue<string>());

        (await _f.RunExportsAsync()).Should().BeGreaterThanOrEqualTo(1);
        var done = await (await admin.GetAsync($"/api/v1/admin/reports/exports/{job["id"]}")).Json();
        done["status"]!.GetValue<string>().Should().Be("Ready");
        done["resultRef"]!.GetValue<string>().Should().StartWith("simulated://reports/postingsbyregion/");
    }

    [Fact]
    public async Task Export_WhenTheSourceFails_ReportsFailed()
    {
        var admin = _f.ClientFor(TestTokens.Admin(Guid.NewGuid()));
        var job = await (await admin.PostJsonAsync("/api/v1/admin/reports/exports", new { reportType = "TopSearches", format = "Json", parameters = new Dictionary<string, string> { ["simulateFailure"] = "true" } })).Json();

        await _f.RunExportsAsync();

        var failed = await (await admin.GetAsync($"/api/v1/admin/reports/exports/{job["id"]}")).Json();
        failed["status"]!.GetValue<string>().Should().Be("Failed");
        failed["failureReason"]!.GetValue<string>().Should().Be("E-AUDIT-REPORT-SOURCE-UNAVAILABLE");
    }

    [Theory]
    [InlineData("Bogus", "Csv")]
    [InlineData("TopSearches", "Pdf")]
    public async Task Export_InvalidTypeOrFormat_Is400(string type, string format)
    {
        var response = await _f.ClientFor(TestTokens.Admin()).PostJsonAsync("/api/v1/admin/reports/exports", new { reportType = type, format });

        await response.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "VAL.INVALID_REQUEST");
    }

    [Fact]
    public async Task Export_ByANonAdministrator_Is403_AndUnknownJobIs404()
    {
        var forbidden = await _f.ClientFor(TestTokens.Employer()).PostJsonAsync("/api/v1/admin/reports/exports", new { reportType = "TopSearches", format = "Csv" });
        await forbidden.ShouldBeProblemAsync(HttpStatusCode.Forbidden, "E-AUM-FORBIDDEN");

        var missing = await _f.ClientFor(TestTokens.Admin()).GetAsync($"/api/v1/admin/reports/exports/{Guid.NewGuid()}");
        await missing.ShouldBeProblemAsync(HttpStatusCode.NotFound, "E-AUDIT-EXPORT-NOT-FOUND");
    }

    // ---------------------------------------------------------------- retention, localisation, plumbing

    [Fact]
    public async Task Errors_AreLocalisedByAcceptLanguage()
    {
        var client = _f.ClientFor(TestTokens.Employer());
        client.DefaultRequestHeaders.AcceptLanguage.ParseAdd("ar");

        var response = await client.GetAsync("/api/v1/admin/audit/access");

        (await response.Json())["detail"]!.GetValue<string>().Should().Be("يمكن للمسؤولين فقط عرض سجل الوصول.");
        var english = await _f.ClientFor(TestTokens.Employer()).GetAsync("/api/v1/admin/audit/access");
        (await english.Json())["detail"]!.GetValue<string>().Should().Be("Only administrators may view the access log.");
    }

    [Fact]
    public async Task HealthOpenApiAndCorrelationHeader_AreServed()
    {
        var anonymous = _f.ClientFor(null);

        (await anonymous.GetAsync("/health/live")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await anonymous.GetAsync("/health/ready")).StatusCode.Should().Be(HttpStatusCode.OK);
        var openApi = await anonymous.GetAsync("/openapi/v1.json");
        openApi.StatusCode.Should().Be(HttpStatusCode.OK);
        var paths = (await openApi.Json())["paths"]!.AsObject().Select(p => p.Key).ToList();
        paths.Should().Contain(new[] { "/api/v1/partners/me/api-responses", "/api/v1/admin/reports/exports", "/api/v1/employers/me/dashboard" });
        var response = await _f.ClientFor(TestTokens.Admin()).GetAsync("/api/v1/admin/audit/access");
        response.Headers.Contains("X-Correlation-Id").Should().BeTrue();
    }

    [Fact]
    public async Task EveryConsumedEventContract_IsHandledByTheInbox()
    {
        // Contract check: each of the 19 catalogued events plus the audit-record stream must have a registered handler for its EventType.
        var registry = _f.Services.GetRequiredService<JobPlatform.BuildingBlocks.Infrastructure.Persistence.InboxHandlerRegistry>();
        var expected = new[]
        {
            "AccountCreated", "AccountApproved", "UserAccountApproved", "ApiCredentialCreated", "ProfileCreated", "EmployerRegistrationApproved", "EmployerVerificationApproved",
            "GovernmentVerificationDataImported", "JobDataImported", "JobPostAttributionUpdated", "JobPostingUpdated", "JobPostingStatusUpdated", "CandidateInsightComputed",
            "TalentPoolEntryCreated", "NotificationSent", "NotificationStatusUpdated", "PlatformEntityRecordCreated", "PlatformTaxonomyUpdated", "JobOfferingSuspended", "AuditRecord"
        };

        foreach (var type in expected)
        {
            registry.Find("audit-logging", type).Should().NotBeNull($"{type} must be consumed by BC-07");
        }
    }
}

/// <summary>Own factory: the test advances the fake clock by more than a year, which would expire every token of a shared fixture.</summary>
public class RetentionApiTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _f;

    public RetentionApiTests(ApiFactory factory) => _f = factory;

    private DateTime Now => _f.Clock.GetUtcNow().UtcDateTime;

    [Fact]
    [Trait("Story", "US-3.1.3-06")]
    [Trait("AC", "AC-04")]
    public async Task Retention_ArchivesExpiredEntries_HiddenByDefault_ButKept()
    {
        var p = Guid.NewGuid();
        await _f.IngestAsync(new AuditRecordIntegrationEvent(Guid.NewGuid(), Now, Guid.NewGuid(), null, "external-integration", "ApiCall", p, "Request", "r-1", $"Partner:{p}", "Post", "Success", "200", null));
        _f.Clock.Advance(TimeSpan.FromDays(400));
        var token = TestTokens.Issue(ActorType.ExternalJobSite, p, clientId: "partner-test", nowUtc: Now);

        (await _f.RunRetentionAsync()).Should().BeGreaterThanOrEqualTo(1);

        var client = _f.ClientFor(token);
        (await (await client.GetAsync("/api/v1/partners/me/api-responses")).Json())["totalCount"]!.GetValue<int>().Should().Be(0);
        var withArchived = await (await client.GetAsync("/api/v1/partners/me/api-responses?includeArchived=true")).Json();
        withArchived["totalCount"]!.GetValue<int>().Should().Be(1);
        withArchived["items"]![0]!["isArchived"]!.GetValue<bool>().Should().BeTrue();
        (await _f.WithDbAsync<AuditDbContext, int>(db => db.AuditEntries.CountAsync(e => e.OwnerId == p))).Should().Be(1, "entries are never purged");
    }

}
