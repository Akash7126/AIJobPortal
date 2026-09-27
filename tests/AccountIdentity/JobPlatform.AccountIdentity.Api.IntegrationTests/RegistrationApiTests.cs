using System.Diagnostics;
using System.Net;
using System.Text.Json.Nodes;
using JobPlatform.AccountIdentity.Domain.Common;
using JobPlatform.BuildingBlocks.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace JobPlatform.AccountIdentity.Api.IntegrationTests;

internal static class OutboxHelpers
{
    public static Task<List<OutboxMessage>> OutboxAsync(this ApiFactory factory, string? type = null, string? aggregateId = null) =>
        factory.WithDbAsync(db => db.Set<OutboxMessage>().AsNoTracking()
            .Where(m => (type == null || m.Type == type) && (aggregateId == null || m.AggregateId == aggregateId))
            .OrderBy(m => m.OccurredOnUtc).ThenBy(m => m.AggregateVersion).ToListAsync());

    public static JsonNode PayloadOf(this OutboxMessage message) => JsonNode.Parse(message.Payload)!;

    public static async Task<JsonNode> PublishedEventAsync(this ApiFactory factory, string type, Guid aggregateId)
    {
        await factory.PublishOutboxAsync();
        var message = factory.Bus.Messages.Last(m => m.Type == type && m.Payload.Contains(aggregateId.ToString()));
        return JsonNode.Parse(message.Payload)!;
    }
}

public class RegistrationApiTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public RegistrationApiTests(ApiFactory factory) => _factory = factory;

    private static object JobSeeker(string? mobile = null, string? email = null, string password = "Str0ngPass") =>
        new { fullName = "Sara Ali", mobile = mobile ?? ApiClient.NewMobile(), email = email ?? ApiClient.NewEmail(), password };

    // ----------------------------------------------------------------------------- US-3.1.1-01 job seeker

    [Fact]
    [Trait("Story", "US-3.1.1-01")]
    [Trait("AC", "AC-01")]
    public async Task RegisterJobSeeker_WithValidDetails_Returns201WithPendingAccount()
    {
        var client = new ApiClient(_factory);

        var response = await client.PostAsync("/api/v1/accounts/job-seekers", JobSeeker());

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location.Should().NotBeNull();
        var body = await response.Json();
        body["standing"]!.GetValue<string>().Should().Be("Pending");
        body["actorType"]!.GetValue<string>().Should().Be("JobSeeker");
        body["nextStep"]!.GetValue<string>().Should().Be("ActivateWithMobileCode");
        body["activationCodeExpiresAtUtc"].Should().NotBeNull();
        body["accountId"]!.GetValue<Guid>().Should().NotBeEmpty();
    }

    [Fact]
    [Trait("Story", "US-3.1.1-01")]
    [Trait("AC", "AC-01")]
    public async Task RegisterJobSeeker_NeverReturnsOrLogsTheOtpPasswordOrHash()
    {
        var client = new ApiClient(_factory);
        var mobile = ApiClient.NewMobile();
        const string password = "Sup3rSecretPw!";

        var response = await client.PostAsync("/api/v1/accounts/job-seekers", JobSeeker(mobile, password: password));
        var raw = await response.Content.ReadAsStringAsync();
        var code = client.LastCode(mobile);

        raw.Should().NotContain(password).And.NotContain(code).And.NotContain("pbkdf2").And.NotContain("passwordHash");
        var hashes = (await _factory.WithDbAsync(db => db.Accounts.AsNoTracking().Select(a => a.PasswordHash).ToListAsync())).Select(h => h.Value).ToList();
        hashes.Should().NotBeEmpty();
        _factory.Logs.Lines.Should().NotContain(l => l.Contains(password) || l.Contains(code) || hashes.Any(h => l.Contains(h)));
    }

    [Fact]
    [Trait("Story", "US-3.1.1-01")]
    [Trait("AC", "AC-02")]
    public async Task RegisterJobSeeker_DuplicateMobile_Returns409Duplicate()
    {
        var client = new ApiClient(_factory);
        var mobile = ApiClient.NewMobile();
        (await client.PostAsync("/api/v1/accounts/job-seekers", JobSeeker(mobile))).EnsureSuccessStatusCode();

        var again = await new ApiClient(_factory).PostAsync("/api/v1/accounts/job-seekers", JobSeeker(mobile));

        await again.ShouldBeProblemAsync(HttpStatusCode.Conflict, "E-JSRPM-DUPLICATE");
        (await again.Json())["ruleCode"]!.GetValue<string>().Should().Be("AI.Account.DUPLICATE");
    }

    [Fact]
    [Trait("Story", "US-3.1.1-01")]
    [Trait("AC", "AC-02")]
    public async Task RegisterJobSeeker_DuplicateEmail_Returns409Duplicate()
    {
        var email = ApiClient.NewEmail();
        (await new ApiClient(_factory).PostAsync("/api/v1/accounts/job-seekers", JobSeeker(email: email))).EnsureSuccessStatusCode();

        var again = await new ApiClient(_factory).PostAsync("/api/v1/accounts/job-seekers", JobSeeker(email: email.ToUpperInvariant()));

        await again.ShouldBeProblemAsync(HttpStatusCode.Conflict, "E-JSRPM-DUPLICATE");
    }

    [Fact]
    [Trait("Story", "US-3.1.1-01")]
    [Trait("AC", "AC-02")]
    public async Task RegisterJobSeeker_SameMobileAsAnEmployer_IsNotADuplicate()
    {
        var mobile = ApiClient.NewMobile();
        (await new ApiClient(_factory).PostAsync("/api/v1/accounts/employers", Employer(mobile: mobile))).EnsureSuccessStatusCode();

        var response = await new ApiClient(_factory).PostAsync("/api/v1/accounts/job-seekers", JobSeeker(mobile));

        response.StatusCode.Should().Be(HttpStatusCode.Created, "uniqueness is per actor type");
    }

    [Fact]
    [Trait("Story", "US-3.1.1-01")]
    [Trait("AC", "AC-03")]
    public async Task RegisterJobSeeker_SixthAttemptWithinFifteenMinutes_IsBlockedWith429()
    {
        var client = new ApiClient(_factory);
        for (var i = 0; i < 5; i++)
        {
            (await client.PostAsync("/api/v1/accounts/job-seekers", JobSeeker())).StatusCode.Should().Be(HttpStatusCode.Created);
        }

        var sixth = await client.PostAsync("/api/v1/accounts/job-seekers", JobSeeker());

        await sixth.ShouldBeProblemAsync(HttpStatusCode.TooManyRequests, "E-JSRPM-RATE-LIMITED");
        sixth.Headers.RetryAfter.Should().NotBeNull();

        _factory.Clock.Advance(TimeSpan.FromMinutes(16));
        (await client.PostAsync("/api/v1/accounts/job-seekers", JobSeeker())).StatusCode.Should().Be(HttpStatusCode.Created, "the window has passed");
    }

    [Fact]
    [Trait("Story", "US-3.1.1-01")]
    [Trait("AC", "AC-03")]
    public async Task RegisterJobSeeker_RateLimitIsPerSource()
    {
        var noisy = new ApiClient(_factory);
        for (var i = 0; i < 6; i++)
        {
            await noisy.PostAsync("/api/v1/accounts/job-seekers", JobSeeker());
        }

        var other = await new ApiClient(_factory).PostAsync("/api/v1/accounts/job-seekers", JobSeeker());

        other.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    [Trait("Story", "US-3.1.1-01")]
    [Trait("AC", "AC-04")]
    public async Task RegisterJobSeeker_WritesAccountCreatedToTheOutboxAndPublishesIt()
    {
        var client = new ApiClient(_factory);
        var mobile = ApiClient.NewMobile();
        var email = ApiClient.NewEmail();
        var (id, _, _) = await client.RegisterJobSeekerAsync(mobile, email);

        var rows = await _factory.OutboxAsync("AccountCreated", id.ToString());

        var row = rows.Should().ContainSingle().Which;
        row.Status.Should().Be(OutboxStatus.Pending);
        row.Exchange.Should().Be("jobplatform.account-identity.events");
        row.RoutingKey.Should().Be("account.created.v1");
        row.AggregateVersion.Should().Be(1);
        var payload = row.PayloadOf();
        payload["accountId"]!.GetValue<Guid>().Should().Be(id);
        payload["actorId"]!.GetValue<Guid>().Should().Be(id);
        payload["actorType"]!.GetValue<string>().Should().Be("JobSeeker");
        payload["aggregateVersion"]!.GetValue<long>().Should().Be(1);
        payload["messageId"]!.GetValue<Guid>().Should().Be(row.Id);
        payload["correlationId"].Should().NotBeNull();
        row.Payload.Should().NotContain(mobile).And.NotContain(email).And.NotContain("assword", "events never carry PII or credentials");

        var published = await _factory.PublishedEventAsync("AccountCreated", id);
        published["actorType"]!.GetValue<string>().Should().Be("JobSeeker");
        var after = await _factory.OutboxAsync("AccountCreated", id.ToString());
        after.Single().Status.Should().Be(OutboxStatus.Published);
        var message = _factory.Bus.Messages.Last(m => m.MessageId == row.Id);
        message.Headers["type"].Should().Be("AccountCreated");
        message.Headers["producer"].Should().Be("account-identity");
        message.Headers["message-id"].Should().Be(row.Id.ToString());
    }

    [Fact]
    [Trait("Story", "US-3.1.1-01")]
    [Trait("AC", "AC-04")]
    public async Task RegisterJobSeeker_WhenRejected_LeavesNoAccountAndNoOutboxRow()
    {
        var mobile = ApiClient.NewMobile();
        (await new ApiClient(_factory).PostAsync("/api/v1/accounts/job-seekers", JobSeeker(mobile))).EnsureSuccessStatusCode();
        var before = (await _factory.OutboxAsync("AccountCreated")).Count;

        (await new ApiClient(_factory).PostAsync("/api/v1/accounts/job-seekers", JobSeeker(mobile))).StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await new ApiClient(_factory).PostAsync("/api/v1/accounts/job-seekers", JobSeeker(password: "weak"))).StatusCode.Should().Be(HttpStatusCode.BadRequest);

        (await _factory.OutboxAsync("AccountCreated")).Count.Should().Be(before, "the event exists only when the aggregate change committed");
    }

    [Fact]
    [Trait("Story", "US-3.1.1-01")]
    [Trait("AC", "AC-05")]
    public async Task RegisterJobSeeker_P95ResponseTime_IsWellUnderThreeSeconds()
    {
        var timings = new List<long>();
        for (var i = 0; i < 20; i++)
        {
            var client = new ApiClient(_factory);
            var stopwatch = Stopwatch.StartNew();
            (await client.PostAsync("/api/v1/accounts/job-seekers", JobSeeker())).EnsureSuccessStatusCode();
            timings.Add(stopwatch.ElapsedMilliseconds);
        }

        var p95 = timings.OrderBy(t => t).ElementAt((int)Math.Ceiling(timings.Count * 0.95) - 1);
        p95.Should().BeLessThan(3000, "A-02-002 requires p95 <= 3 s");
    }

    [Fact]
    [Trait("Story", "US-3.1.1-01")]
    [Trait("AC", "AC-06")]
    public async Task RegisterJobSeeker_ErrorMessagesAreAvailableInArabicAndEnglish()
    {
        var mobile = ApiClient.NewMobile();
        (await new ApiClient(_factory).PostAsync("/api/v1/accounts/job-seekers", JobSeeker(mobile))).EnsureSuccessStatusCode();

        var arabic = await new ApiClient(_factory).WithLanguage("ar").PostAsync("/api/v1/accounts/job-seekers", JobSeeker(mobile));
        var english = await new ApiClient(_factory).WithLanguage("en").PostAsync("/api/v1/accounts/job-seekers", JobSeeker(mobile));
        var fallback = await new ApiClient(_factory).WithLanguage("fr").PostAsync("/api/v1/accounts/job-seekers", JobSeeker(mobile));

        var arabicDetail = (await arabic.Json())["detail"]!.GetValue<string>();
        var englishDetail = (await english.Json())["detail"]!.GetValue<string>();
        arabicDetail.Should().MatchRegex("\\p{IsArabic}").And.NotBe(englishDetail);
        englishDetail.Should().Contain("already exists");
        (await fallback.Json())["detail"]!.GetValue<string>().Should().Be(englishDetail);
        (await arabic.Json())["code"]!.GetValue<string>().Should().Be("E-JSRPM-DUPLICATE", "codes are stable across languages");
    }

    [Fact]
    [Trait("Story", "US-3.1.1-01")]
    [Trait("AC", "AC-07")]
    public async Task RegisterJobSeeker_ErrorsAreMachineReadable_SoAssistiveClientsCanAnnounceEachField()
    {
        // WCAG 2.1 AA is a UI conformance target verified by review and UI tests (see README). What the API owes accessible
        // clients is field-level, machine-readable errors with stable codes so labels/announcements can be rendered per field.
        var response = await new ApiClient(_factory).PostAsync("/api/v1/accounts/job-seekers", new { fullName = "", mobile = "nope", email = "bad", password = "" });

        await response.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "VAL.INVALID_REQUEST");
        var errors = (await response.Json())["errors"]!.AsObject();
        errors["fullName"]!.AsArray().Select(e => e!.GetValue<string>()).Should().Contain("VAL.FullName.Required");
        errors["mobile"]!.AsArray().Select(e => e!.GetValue<string>()).Should().Contain("VAL.MobileNumber.Invalid");
        errors["email"]!.AsArray().Select(e => e!.GetValue<string>()).Should().Contain("VAL.Email.Invalid");
        errors["password"]!.AsArray().Select(e => e!.GetValue<string>()).Should().Contain("VAL.Password.Required");
    }

    [Fact]
    [Trait("Story", "US-3.1.1-01")]
    [Trait("AC", "AC-07")]
    public void ErrorCatalogue_EveryPublishedCodeHasArabicAndEnglishText()
    {
        var localizer = new JobPlatform.AccountIdentity.Api.Security.ResourceErrorMessageLocalizer();
        var codes = typeof(ErrorCodes).GetFields().Where(f => f.IsLiteral).Select(f => (string)f.GetRawConstantValue()!).ToList();

        codes.Should().NotBeEmpty();
        foreach (var code in codes)
        {
            var en = localizer.Localize(code, "<<missing>>", JobPlatform.SharedKernel.Common.Enums.Language.En);
            var ar = localizer.Localize(code, "<<missing>>", JobPlatform.SharedKernel.Common.Enums.Language.Ar);
            en.Should().NotBe("<<missing>>", $"{code} needs an English message");
            ar.Should().NotBe("<<missing>>", $"{code} needs an Arabic message");
            ar.Should().NotBe(en);
        }
    }

    [Fact]
    [Trait("Story", "US-3.1.1-01")]
    [Trait("AC", "AC-01")]
    public async Task RegisterJobSeeker_InvalidInputs_ReturnFieldLevelValidationErrors()
    {
        var client = new ApiClient(_factory);

        var badMobile = await client.PostAsync("/api/v1/accounts/job-seekers", JobSeeker(mobile: "0591234567"));
        await badMobile.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "VAL.INVALID_REQUEST");
        (await badMobile.Json())["errors"]!["mobile"]!.AsArray().Select(e => e!.GetValue<string>()).Should().Contain("VAL.MobileNumber.Invalid");

        var longName = await client.PostAsync("/api/v1/accounts/job-seekers", new { fullName = new string('x', 201), mobile = ApiClient.NewMobile(), password = "Str0ngPass" });
        (await longName.Json())["errors"]!["fullName"]!.AsArray().Select(e => e!.GetValue<string>()).Should().Contain("VAL.FullName.TooLong");

        var language = await client.PostAsync("/api/v1/accounts/job-seekers", new { fullName = "A", mobile = ApiClient.NewMobile(), password = "Str0ngPass", preferredLanguage = "de" });
        (await language.Json())["errors"]!["preferredLanguage"].Should().NotBeNull();
    }

    [Fact]
    [Trait("Story", "US-3.1.5-02")]
    [Trait("AC", "AC-02")]
    public async Task RegisterJobSeeker_WeakPassword_Returns400InvalidFieldWithViolations()
    {
        var response = await new ApiClient(_factory).PostAsync("/api/v1/accounts/job-seekers", JobSeeker(password: "short"));

        await response.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "E-AAFR-INVALID-FIELD");
        var violations = (await response.Json())["errors"]!["password"]!.AsArray().Select(v => v!.GetValue<string>()).ToList();
        violations.Should().Contain(new[] { "VAL.Password.MinLength", "VAL.Password.Uppercase", "VAL.Password.Digit" });
    }

    [Fact]
    public async Task RegisterJobSeeker_MalformedJson_Returns400Problem()
    {
        var response = await new ApiClient(_factory).Http.PostAsync("/api/v1/accounts/job-seekers",
            new StringContent("{ not json", System.Text.Encoding.UTF8, "application/json"));

        await response.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "VAL.INVALID_REQUEST");
    }

    [Fact]
    public async Task RegisterJobSeeker_SameIdempotencyKeyAndPayload_ReplaysTheStoredResponse()
    {
        var client = new ApiClient(_factory);
        var payload = JobSeeker();
        var key = Guid.NewGuid().ToString();

        var first = await client.PostAsync("/api/v1/accounts/job-seekers", payload, key);
        var second = await client.PostAsync("/api/v1/accounts/job-seekers", payload, key);

        first.StatusCode.Should().Be(HttpStatusCode.Created);
        second.StatusCode.Should().Be(HttpStatusCode.Created, "a replay must not turn into a duplicate error");
        (await second.Json())["accountId"]!.GetValue<Guid>().Should().Be((await first.Json())["accountId"]!.GetValue<Guid>());
        (await _factory.OutboxAsync("AccountCreated", (await first.Json())["accountId"]!.GetValue<string>())).Should().HaveCount(1);
    }

    [Fact]
    public async Task RegisterJobSeeker_IdempotencyKeyReusedWithDifferentPayload_Returns422()
    {
        var client = new ApiClient(_factory);
        var key = Guid.NewGuid().ToString();
        (await client.PostAsync("/api/v1/accounts/job-seekers", JobSeeker(), key)).EnsureSuccessStatusCode();

        var other = await client.PostAsync("/api/v1/accounts/job-seekers", JobSeeker(), key);

        await other.ShouldBeProblemAsync(HttpStatusCode.UnprocessableEntity, "E-IDEMPOTENCY-KEY-REUSED");
    }

    // ----------------------------------------------------------------------------- US-3.1.2-01 employer

    private static object Employer(string? mobile = null, string? companyId = null, int level = 1, string? email = null) => new
    {
        companyName = "Acme Trading",
        email = email ?? ApiClient.NewEmail(),
        mobile = mobile ?? ApiClient.NewMobile(),
        companyId = companyId ?? "C-" + Guid.NewGuid().ToString("N"),
        registrationNumber = "REG-001",
        password = "Str0ngPass",
        level
    };

    [Fact]
    [Trait("Story", "US-3.1.2-01")]
    [Trait("AC", "AC-01")]
    public async Task RegisterEmployer_LevelOneDetails_Returns201Pending()
    {
        var response = await new ApiClient(_factory).PostAsync("/api/v1/accounts/employers", Employer());

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Json();
        body["actorType"]!.GetValue<string>().Should().Be("Employer");
        body["standing"]!.GetValue<string>().Should().Be("Pending");
        var id = body["accountId"]!.GetValue<Guid>();
        var stored = await _factory.WithDbAsync(db => db.Accounts.AsNoTracking().FirstAsync(a => a.Id == new AccountId(id)));
        stored.RegistrationNumber.Should().Be("REG-001");
        stored.IdentityKey!.Value.Should().StartWith("C-");
    }

    [Theory]
    [InlineData(1, HttpStatusCode.Created)]
    [InlineData(2, HttpStatusCode.Created)]
    [InlineData(3, HttpStatusCode.BadRequest)]
    [InlineData(0, HttpStatusCode.BadRequest)]
    [Trait("Story", "US-3.1.2-01")]
    [Trait("AC", "AC-02")]
    public async Task RegisterEmployer_RegistrationLevels_OneAndTwoAreAccepted(int level, HttpStatusCode expected)
    {
        var response = await new ApiClient(_factory).PostAsync("/api/v1/accounts/employers", Employer(level: level));

        response.StatusCode.Should().Be(expected);
    }

    [Fact]
    [Trait("Story", "US-3.1.2-01")]
    [Trait("AC", "AC-01")]
    public async Task RegisterEmployer_MissingLevelOneFields_ReturnsFieldErrors()
    {
        var response = await new ApiClient(_factory).PostAsync("/api/v1/accounts/employers",
            new { companyName = "", email = "", mobile = "", companyId = "", registrationNumber = "", password = "", level = 1 });

        var errors = (await response.Json())["errors"]!.AsObject();
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        errors.Select(e => e.Key).Should().Contain(new[] { "companyName", "email", "mobile", "companyId", "registrationNumber", "password" });
    }

    [Fact]
    [Trait("Story", "US-3.1.2-01")]
    [Trait("AC", "AC-03")]
    public async Task RegisterEmployer_DuplicateCompanyId_Returns409EmployerDuplicate()
    {
        var companyId = "C-" + Guid.NewGuid().ToString("N");
        (await new ApiClient(_factory).PostAsync("/api/v1/accounts/employers", Employer(companyId: companyId))).EnsureSuccessStatusCode();

        var again = await new ApiClient(_factory).PostAsync("/api/v1/accounts/employers", Employer(companyId: companyId.ToLowerInvariant()));

        await again.ShouldBeProblemAsync(HttpStatusCode.Conflict, "E-ERPM-DUPLICATE");
    }

    [Fact]
    [Trait("Story", "US-3.1.2-01")]
    [Trait("AC", "AC-04")]
    public async Task RegisterEmployer_SixthAttempt_IsBlockedWithEmployerRateLimitedCode()
    {
        var client = new ApiClient(_factory);
        for (var i = 0; i < 5; i++)
        {
            (await client.PostAsync("/api/v1/accounts/employers", Employer())).EnsureSuccessStatusCode();
        }

        var sixth = await client.PostAsync("/api/v1/accounts/employers", Employer());

        await sixth.ShouldBeProblemAsync(HttpStatusCode.TooManyRequests, "E-ERPM-RATE-LIMITED");
    }

    [Fact]
    [Trait("Story", "US-3.1.2-01")]
    [Trait("AC", "AC-05")]
    public async Task RegisterEmployer_PublishesAccountCreatedWithEmployerActorType()
    {
        var response = await new ApiClient(_factory).PostAsync("/api/v1/accounts/employers", Employer());
        var id = (await response.Json())["accountId"]!.GetValue<Guid>();

        var published = await _factory.PublishedEventAsync("AccountCreated", id);

        published["actorType"]!.GetValue<string>().Should().Be("Employer", "BC-01 needs to know it is an employer");
    }

    // ----------------------------------------------------------------------------- US-3.1.3-01 partner

    private static object Partner(string? identity = null) => new
    {
        organisationName = "Jobs Partner Ltd",
        contactEmail = ApiClient.NewEmail(),
        mobile = ApiClient.NewMobile(),
        identity = identity ?? "partner-" + Guid.NewGuid().ToString("N"),
        password = "Str0ngPass"
    };

    [Fact]
    [Trait("Story", "US-3.1.3-01")]
    [Trait("AC", "AC-01")]
    public async Task RegisterPartner_ValidDetails_Returns201PendingAwaitingStaffApproval()
    {
        var response = await new ApiClient(_factory).PostAsync("/api/v1/accounts/external-job-sites", Partner());

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Json();
        body["actorType"]!.GetValue<string>().Should().Be("ExternalJobSite");
        body["standing"]!.GetValue<string>().Should().Be("Pending");
        body["nextStep"]!.GetValue<string>().Should().Be("AwaitStaffApproval");
        body["activationCodeExpiresAtUtc"].Should().BeNull("partners are approved by staff, no mobile code is issued");
    }

    [Fact]
    [Trait("Story", "US-3.1.3-01")]
    [Trait("AC", "AC-02")]
    public async Task RegisterPartner_DuplicateIdentity_Returns409PartnerDuplicate()
    {
        var identity = "partner-" + Guid.NewGuid().ToString("N");
        (await new ApiClient(_factory).PostAsync("/api/v1/accounts/external-job-sites", Partner(identity))).EnsureSuccessStatusCode();

        var again = await new ApiClient(_factory).PostAsync("/api/v1/accounts/external-job-sites", Partner(identity));

        await again.ShouldBeProblemAsync(HttpStatusCode.Conflict, "E-TPJPRI-DUPLICATE");
    }

    [Fact]
    [Trait("Story", "US-3.1.3-01")]
    [Trait("AC", "AC-03")]
    public async Task RegisterPartner_PublishesAccountCreatedWithPartnerActorType()
    {
        var response = await new ApiClient(_factory).PostAsync("/api/v1/accounts/external-job-sites", Partner());
        var id = (await response.Json())["accountId"]!.GetValue<Guid>();

        (await _factory.PublishedEventAsync("AccountCreated", id))["actorType"]!.GetValue<string>().Should().Be("ExternalJobSite");
    }

    [Fact]
    [Trait("Story", "US-3.1.3-01")]
    [Trait("AC", "AC-04")]
    public async Task ApprovePartner_ByAuthorisedStaff_ActivatesThePartnerAndPublishesAccountApproved()
    {
        var register = await new ApiClient(_factory).PostAsync("/api/v1/accounts/external-job-sites", Partner());
        var id = (await register.Json())["accountId"]!.GetValue<Guid>();
        var admin = await new ApiClient(_factory).AdminAsync();

        var response = await admin.PostAsync($"/api/v1/accounts/{id}/approve-partner");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var summary = await (await (await new ApiClient(_factory).ServiceAsync()).Http.GetAsync($"/internal/v1/accounts/{id}")).Json();
        summary["standing"]!.GetValue<string>().Should().Be("Active");
        var approved = await _factory.PublishedEventAsync("AccountApproved", id);
        approved["actorType"]!.GetValue<string>().Should().Be("ExternalJobSite");
    }

    [Fact]
    [Trait("Story", "US-3.1.3-01")]
    [Trait("AC", "AC-04")]
    public async Task ApprovePartner_ByNonAuthorisedUser_Returns403()
    {
        var register = await new ApiClient(_factory).PostAsync("/api/v1/accounts/external-job-sites", Partner());
        var id = (await register.Json())["accountId"]!.GetValue<Guid>();
        var mobile = ApiClient.NewMobile();
        var seeker = new ApiClient(_factory);
        await seeker.ActiveJobSeekerAsync(mobile);
        var seekerClient = await seeker.UserAsync(mobile, "Str0ngPass");

        var asSeeker = await seekerClient.PostAsync($"/api/v1/accounts/{id}/approve-partner");
        var anonymous = await new ApiClient(_factory).PostAsync($"/api/v1/accounts/{id}/approve-partner");

        await asSeeker.ShouldBeProblemAsync(HttpStatusCode.Forbidden, "E-AUM-FORBIDDEN");
        anonymous.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    [Trait("Story", "US-3.1.3-01")]
    [Trait("AC", "AC-04")]
    public async Task ApprovePartner_WhenTheAdministratorLacksThePermission_IsRefusedByTheApplicationLayer()
    {
        var register = await new ApiClient(_factory).PostAsync("/api/v1/accounts/external-job-sites", Partner());
        var id = (await register.Json())["accountId"]!.GetValue<Guid>();
        var admin = await new ApiClient(_factory).AdminAsync();
        (await admin.Http.DeleteAsync($"/api/v1/admin/roles/{JobPlatform.AccountIdentity.Domain.Rbac.WellKnownRoles.Administrator.Value}/permissions/accounts.approve-partner"))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        var response = await admin.PostAsync($"/api/v1/accounts/{id}/approve-partner");

        await response.ShouldBeProblemAsync(HttpStatusCode.Forbidden, "E-AUM-FORBIDDEN");
        (await admin.Http.PutAsync($"/api/v1/admin/roles/{JobPlatform.AccountIdentity.Domain.Rbac.WellKnownRoles.Administrator.Value}/permissions/accounts.approve-partner", null))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await admin.PostAsync($"/api/v1/accounts/{id}/approve-partner")).StatusCode.Should().Be(HttpStatusCode.NoContent);
    }
}
