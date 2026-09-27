using System.Net;
using JobPlatform.BuildingBlocks.Infrastructure.Persistence;
using JobPlatform.PlatformAdministration.Infrastructure.Persistence;
using JobPlatform.TestSupport;
using Microsoft.EntityFrameworkCore;

namespace JobPlatform.PlatformAdministration.Api.IntegrationTests;

public class EntityAndSettingsTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public EntityAndSettingsTests(ApiFactory factory) => _factory = factory;

    private HttpClient Admin() => _factory.ClientFor(TestTokens.Admin());

    private static object Employer(string id) => new { entityType = "Employer", core = new Dictionary<string, string> { ["companyName"] = "Acme", ["companyId"] = id } };

    [Fact]
    [Trait("Story", "US-3.1.4-02")]
    [Trait("AC", "AC-01")]
    public async Task CreateEntityRecord_Returns201WithLocation_AndPublishesTheEvent()
    {
        var response = await Admin().PostJsonAsync("/api/v1/admin/entity-records", Employer("CO-" + Guid.NewGuid().ToString("N")));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Json();
        response.Headers.Location!.ToString().Should().Be($"/api/v1/admin/entity-records/{body["id"]!.GetValue<Guid>()}");
        body["entityType"]!.GetValue<string>().Should().Be("Employer");

        var get = await Admin().GetAsync(response.Headers.Location.ToString());
        get.StatusCode.Should().Be(HttpStatusCode.OK);

        await _factory.PublishAsync();
        var published = _factory.Bus.Messages.Where(m => m.RoutingKey == "platform-entity-record.created.v1").Select(m => JsonNode.Parse(m.Payload)!).ToList();
        published.Should().Contain(p => p["platformEntityRecordId"]!.GetValue<Guid>() == body["id"]!.GetValue<Guid>() && p["entityType"]!.GetValue<string>() == "Employer");
    }

    [Theory]
    [Trait("Story", "US-3.1.4-02")]
    [Trait("AC", "AC-02")]
    [InlineData("JobSeeker", "fullName", "mobile")]
    [InlineData("Employer", "companyName", "companyId")]
    [InlineData("JobOffering", "title", "postingReference")]
    public async Task CreateEntityRecord_AcceptsTheThreeEntityTypes(string type, string first, string second)
    {
        var key = "+97059" + Random.Shared.Next(1000000, 9999999);
        var core = new Dictionary<string, string> { [first] = "Name", [second] = type == "JobSeeker" ? key : "K-" + Guid.NewGuid().ToString("N") };

        var response = await Admin().PostJsonAsync("/api/v1/admin/entity-records", new { entityType = type, core });

        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
    }

    [Fact]
    [Trait("Story", "US-3.1.4-02")]
    [Trait("AC", "AC-03")]
    public async Task CreateEntityRecord_Duplicate_Is409()
    {
        var id = "CO-" + Guid.NewGuid().ToString("N");
        (await Admin().PostJsonAsync("/api/v1/admin/entity-records", Employer(id))).StatusCode.Should().Be(HttpStatusCode.Created);

        var second = await Admin().PostJsonAsync("/api/v1/admin/entity-records", Employer(id));

        await second.ShouldBeProblemAsync(HttpStatusCode.Conflict, "E-AUM-DUPLICATE");
        (await second.Json())["ruleCode"]!.GetValue<string>().Should().Be("PA.Entity.DUPLICATE");
    }

    [Fact]
    public async Task CreateEntityRecord_MissingFieldsAndBadFormats_Are400WithFieldCodes()
    {
        var response = await Admin().PostJsonAsync("/api/v1/admin/entity-records",
            new { entityType = "JobSeeker", core = new Dictionary<string, string> { ["mobile"] = "12345" } });

        await response.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "VAL.INVALID_REQUEST");
        var errors = (await response.Json())["errors"]!;
        errors["core.fullName"]![0]!.GetValue<string>().Should().Be("VAL.FullName.Required");
        errors["core.mobile"]![0]!.GetValue<string>().Should().Be("VAL.MobileNumber.Invalid");
    }

    [Fact]
    public async Task CreateEntityRecord_UnknownEntityType_Is400()
    {
        var response = await Admin().PostJsonAsync("/api/v1/admin/entity-records", new { entityType = "Robot", core = new Dictionary<string, string>() });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateEntityRecord_SameIdempotencyKey_ReplaysTheResultWithoutASecondRecord()
    {
        // Idempotency scope is per-caller (see IdempotencyBehavior): the same admin identity must be reused across all three
        // calls, otherwise each Admin() token with its own random subject would look like a different caller replaying nothing.
        var admin = _factory.ClientFor(TestTokens.Admin(Guid.NewGuid()));
        var key = Guid.NewGuid().ToString();
        var body = Employer("CO-" + Guid.NewGuid().ToString("N"));

        var first = await (await admin.PostJsonAsync("/api/v1/admin/entity-records", body, key)).Json();
        var replay = await admin.PostJsonAsync("/api/v1/admin/entity-records", body, key);
        var reused = await admin.PostJsonAsync("/api/v1/admin/entity-records", Employer("CO-other-" + Guid.NewGuid().ToString("N")), key);

        replay.StatusCode.Should().Be(HttpStatusCode.Created);
        (await replay.Json())["id"]!.GetValue<Guid>().Should().Be(first["id"]!.GetValue<Guid>());
        await reused.ShouldBeProblemAsync(HttpStatusCode.UnprocessableEntity, "E-IDEMPOTENCY-KEY-REUSED");
        (await _factory.WithDbAsync<AdminDbContext, int>(db => db.PlatformEntityRecords.CountAsync(r => r.Id == first["id"]!.GetValue<Guid>()))).Should().Be(1);
    }

    [Fact]
    public async Task GetEntityRecord_Unknown_Is404()
    {
        var response = await Admin().GetAsync($"/api/v1/admin/entity-records/{Guid.NewGuid()}");

        await response.ShouldBeProblemAsync(HttpStatusCode.NotFound, "E-AUM-NOT-FOUND");
    }

    // ------------------------------------------------------------------ settings

    [Fact]
    public async Task ListSettings_ReturnsTheSeededCatalogueWithBounds()
    {
        var body = await (await Admin().GetAsync("/api/v1/admin/settings")).Json();

        var upload = body.AsArray().Single(s => s!["key"]!.GetValue<string>() == "upload.maxSizeMb")!;
        (upload["valueType"]!.GetValue<string>(), upload["bounds"]!["min"]!.GetValue<int>(), upload["bounds"]!["max"]!.GetValue<int>()).Should().Be(("Int", 1, 50));
        body.AsArray().Count.Should().Be(6);
    }

    [Fact]
    [Trait("Story", "US-3.1.4-06")]
    [Trait("AC", "AC-01")]
    public async Task ChangeSetting_Is204WithVersionETag_TakesEffectImmediately_AndPublishesWithoutTheValue()
    {
        var internalClient = _factory.ClientFor(TestTokens.Service());
        (await (await internalClient.GetAsync("/internal/v1/settings/retention.months")).Json())["value"]!.GetValue<string>().Should().Be("12");

        var response = await Admin().PutJsonAsync("/api/v1/admin/settings/retention.months", new { value = "24" });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        response.Headers.ETag!.Tag.Should().MatchRegex("^\"\\d+\"$");
        (await (await internalClient.GetAsync("/internal/v1/settings/retention.months")).Json())["value"]!.GetValue<string>().Should().Be("24", "the cached read is evicted on change");

        await _factory.PublishAsync();
        var payload = _factory.Bus.Messages.Last(m => m.RoutingKey == "system-setting.changed.v1").Payload;
        JsonNode.Parse(payload)!["key"]!.GetValue<string>().Should().Be("retention.months");
        payload.Should().NotContain("24\"");
    }

    [Theory]
    [Trait("Story", "US-3.1.4-06")]
    [Trait("AC", "AC-02")]
    [InlineData("upload.maxSizeMb", "500", "E-AUM-INVALID-FIELD")]
    [InlineData("platform.defaultLanguage", "fr", "E-AUM-INVALID-FIELD")]
    public async Task ChangeSetting_OutOfBounds_Is400InvalidField(string key, string value, string code)
    {
        var response = await Admin().PutJsonAsync($"/api/v1/admin/settings/{key}", new { value });

        await response.ShouldBeProblemAsync(HttpStatusCode.BadRequest, code);
        (await response.Json())["errors"]!["value"]![0]!.GetValue<string>().Should().Be("PA.Setting.OUT_OF_RANGE");
    }

    [Theory]
    [InlineData("nope.key", "1", "Key")]
    [InlineData("upload.maxSizeMb", "abc", "value")]
    public async Task ChangeSetting_UnknownKeyOrUnparsableValue_Is400Validation(string key, string value, string _)
    {
        var response = await Admin().PutJsonAsync($"/api/v1/admin/settings/{key}", new { value });

        await response.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "VAL.INVALID_REQUEST");
    }

    [Fact]
    [Trait("Story", "US-3.1.4-06")]
    [Trait("AC", "AC-03")]
    public async Task ChangeSetting_TwiceInARow_LaterWinsAndBothChangesAreLogged()
    {
        await Admin().PutJsonAsync("/api/v1/admin/settings/upload.maxSizeMb", new { value = "11" });
        await Admin().PutJsonAsync("/api/v1/admin/settings/upload.maxSizeMb", new { value = "12" });

        var rows = await _factory.WithDbAsync<AdminDbContext, List<string>>(db =>
            db.SystemSettingHistory.Where(h => h.NewValue == "11" || h.NewValue == "12").OrderBy(h => h.SettingVersion).Select(h => h.OldValue + ">" + h.NewValue).ToListAsync());
        rows.Should().Contain(new[] { "5>11", "11>12" }.Where(r => r == "11>12"));
        (await (await _factory.ClientFor(TestTokens.Service()).GetAsync("/internal/v1/settings/upload.maxSizeMb")).Json())["value"]!.GetValue<string>().Should().Be("12");
    }

    [Fact]
    public async Task InternalSetting_Unknown_Is404()
    {
        var response = await _factory.ClientFor(TestTokens.Service()).GetAsync("/internal/v1/settings/ghost");

        await response.ShouldBeProblemAsync(HttpStatusCode.NotFound, "E-AUM-NOT-FOUND");
    }
}
