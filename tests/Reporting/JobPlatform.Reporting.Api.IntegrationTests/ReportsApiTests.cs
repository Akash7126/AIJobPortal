using System.Net;
using JobPlatform.TestSupport;

namespace JobPlatform.Reporting.Api.IntegrationTests;

public class ActivityApiTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public ActivityApiTests(ApiFactory factory) => _factory = factory;

    [Fact]
    public async Task GetRetentionPolicy_Anonymous_Returns401()
    {
        var response = await _factory.ClientFor(null).GetAsync("/api/v1/admin/reports/activity/retention-policy");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetRetentionPolicy_AsJobSeeker_Returns403()
    {
        var response = await _factory.ClientFor(TestTokens.JobSeeker()).GetAsync("/api/v1/admin/reports/activity/retention-policy");

        await response.ShouldBeProblemAsync(HttpStatusCode.Forbidden, "E-UAM-FORBIDDEN");
    }

    [Fact]
    public async Task GetRetentionPolicy_AsAdministrator_Returns200_WithDefaultMonths()
    {
        var response = await _factory.ClientFor(TestTokens.Admin()).GetAsync("/api/v1/admin/reports/activity/retention-policy");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Json())["retentionMonths"]!.GetValue<int>().Should().BeGreaterThanOrEqualTo(6);
    }

    [Fact]
    public async Task SetRetentionPolicy_ThenGet_RoundTripsTheNewValue()
    {
        var client = _factory.ClientFor(TestTokens.Admin());

        var set = await client.PutJsonAsync("/api/v1/admin/reports/activity/retention-policy", new { months = 24 });
        set.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var get = await client.GetAsync("/api/v1/admin/reports/activity/retention-policy");
        (await get.Json())["retentionMonths"]!.GetValue<int>().Should().Be(24);
    }

    [Fact]
    public async Task GetUserActivity_AsAdministrator_Returns200()
    {
        var response = await _factory.ClientFor(TestTokens.Admin()).GetAsync("/api/v1/admin/reports/activity");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Json())["items"]!.AsArray().Should().NotBeEmpty();
    }
}

public class EmploymentApiTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public EmploymentApiTests(ApiFactory factory) => _factory = factory;

    [Fact]
    public async Task GetStatistics_AsAdministrator_Returns200()
    {
        var response = await _factory.ClientFor(TestTokens.Admin()).GetAsync("/api/v1/admin/reports/employment/statistics");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetStatistics_AsEmployer_Returns403()
    {
        var response = await _factory.ClientFor(TestTokens.Employer()).GetAsync("/api/v1/admin/reports/employment/statistics");

        await response.ShouldBeProblemAsync(HttpStatusCode.Forbidden, "E-EMPST-FORBIDDEN");
    }
}

public class LaborMarketApiTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public LaborMarketApiTests(ApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Generate_ThenGet_RoundTripsTheSamePeriod()
    {
        var client = _factory.ClientFor(TestTokens.Admin());

        var generated = await client.PostJsonAsync("/api/v1/admin/reports/labor-market/generate", new { period = "2026-01" });
        generated.StatusCode.Should().Be(HttpStatusCode.OK);
        var period = (await generated.Json())["period"]!.GetValue<string>();
        period.Should().Be("2026-01");

        var get = await client.GetAsync("/api/v1/admin/reports/labor-market?period=2026-01");
        get.StatusCode.Should().Be(HttpStatusCode.OK);
        (await get.Json())["existing"]!.GetValue<bool>().Should().BeTrue();
    }

    [Fact]
    public async Task Generate_SamePeriodTwice_ReturnsTheSameReport_NotDuplicated()
    {
        var client = _factory.ClientFor(TestTokens.Admin());

        var first = await client.PostJsonAsync("/api/v1/admin/reports/labor-market/generate", new { period = "2026-02" });
        var second = await client.PostJsonAsync("/api/v1/admin/reports/labor-market/generate", new { period = "2026-02" });

        var firstId = (await first.Json())["id"]!.GetValue<Guid>();
        var secondId = (await second.Json())["id"]!.GetValue<Guid>();
        secondId.Should().Be(firstId);
    }
}

public class SystemPerformanceApiTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public SystemPerformanceApiTests(ApiFactory factory) => _factory = factory;

    [Fact]
    public async Task ConfigureAlertRule_ThenListAlertRules_RoundTrips()
    {
        var client = _factory.ClientFor(TestTokens.Admin());

        var configure = await client.PutJsonAsync("/api/v1/admin/reports/system/alert-rules", new
        {
            id = (Guid?)null, metric = "error_rate_percent", comparator = "GreaterThan", threshold = 5, windowMinutes = 5, severity = "Critical", enabled = true
        });
        configure.StatusCode.Should().Be(HttpStatusCode.OK);

        var list = await client.GetAsync("/api/v1/admin/reports/system/alert-rules");
        list.StatusCode.Should().Be(HttpStatusCode.OK);
        (await list.Json()).AsArray().Should().Contain(r => r!["metric"]!.GetValue<string>() == "error_rate_percent");
    }

    [Fact]
    public async Task Performance_AsAdministrator_Returns200()
    {
        var response = await _factory.ClientFor(TestTokens.Admin()).GetAsync("/api/v1/admin/reports/system/performance");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}

public class TemplatesApiTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public TemplatesApiTests(ApiFactory factory) => _factory = factory;

    private static object TemplateBody(string name = "Salary report") => new
    {
        name,
        dataSource = "Employment",
        parameters = new[] { new { name = "minSalary", type = "Decimal", min = "0", max = (string?)null, default_ = (string?)null, options = (string[]?)null } }
    };

    [Fact]
    public async Task Create_ThenGet_ReturnsTheSameTemplate()
    {
        var client = _factory.ClientFor(TestTokens.Admin());

        var created = await client.PostJsonAsync("/api/v1/admin/reports/templates", new { name = "Salary report", dataSource = "Employment", parameters = Array.Empty<object>() });
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        created.Headers.Location.Should().NotBeNull();

        var get = await client.GetAsync(created.Headers.Location);
        get.StatusCode.Should().Be(HttpStatusCode.OK);
        (await get.Json())["name"]!.GetValue<string>().Should().Be("Salary report");
    }

    [Fact]
    public async Task Update_LaterSaveWins_IncrementsRevision()
    {
        var client = _factory.ClientFor(TestTokens.Admin());
        var created = await client.PostJsonAsync("/api/v1/admin/reports/templates", new { name = "Report A", dataSource = "Activity", parameters = Array.Empty<object>() });
        var id = (await created.Json())["id"]!.GetValue<Guid>();

        var updated = await client.PutJsonAsync($"/api/v1/admin/reports/templates/{id}", new { name = "Report A renamed", dataSource = "Activity", parameters = Array.Empty<object>() });
        updated.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var get = await client.GetAsync($"/api/v1/admin/reports/templates/{id}");
        var body = await get.Json();
        body["name"]!.GetValue<string>().Should().Be("Report A renamed");
        body["revision"]!.GetValue<int>().Should().Be(2);
    }

    [Fact]
    public async Task List_ReturnsCreatedTemplates()
    {
        var client = _factory.ClientFor(TestTokens.Admin());
        await client.PostJsonAsync("/api/v1/admin/reports/templates", new { name = "Listable template", dataSource = "Performance", parameters = Array.Empty<object>() });

        var list = await client.GetAsync("/api/v1/admin/reports/templates");

        (await list.Json()).AsArray().Should().Contain(t => t!["name"]!.GetValue<string>() == "Listable template");
    }
}

public class SchedulesApiTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public SchedulesApiTests(ApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Create_List_ThenDelete_ASchedule()
    {
        var client = _factory.ClientFor(TestTokens.Admin());
        var template = await client.PostJsonAsync("/api/v1/admin/reports/templates", new { name = "For schedule", dataSource = "Activity", parameters = Array.Empty<object>() });
        var templateId = (await template.Json())["id"]!.GetValue<Guid>();

        var created = await client.PostJsonAsync("/api/v1/admin/reports/schedules", new
        {
            name = "Weekly", templateId, savedReportId = (Guid?)null, interval = "Weekly", cron = (string?)null, recipients = new[] { "ops@example.com" }, format = "Pdf"
        });
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        var id = (await created.Json())["id"]!.GetValue<Guid>();

        var list = await client.GetAsync("/api/v1/admin/reports/schedules");
        (await list.Json()).AsArray().Should().Contain(s => s!["id"]!.GetValue<Guid>() == id);

        var delete = await client.DeleteAsync($"/api/v1/admin/reports/schedules/{id}");
        delete.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var listAfter = await client.GetAsync("/api/v1/admin/reports/schedules");
        (await listAfter.Json()).AsArray().Should().NotContain(s => s!["id"]!.GetValue<Guid>() == id);
    }
}

public class SavedReportsApiTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public SavedReportsApiTests(ApiFactory factory) => _factory = factory;

    private static object Definition() => new { dataSource = "Activity", fields = new[] { "eventType", "events" }, filters = Array.Empty<object>(), groupBy = Array.Empty<string>() };

    [Fact]
    public async Task Save_ThenRun_ThenDelete()
    {
        var client = _factory.ClientFor(TestTokens.Admin());

        var saved = await client.PostJsonAsync("/api/v1/admin/reports/saved", new { name = "My saved report", definition = Definition() });
        saved.StatusCode.Should().Be(HttpStatusCode.Created);
        var id = (await saved.Json())["id"]!.GetValue<Guid>();

        var list = await client.GetAsync("/api/v1/admin/reports/saved");
        (await list.Json()).AsArray().Should().Contain(r => r!["id"]!.GetValue<Guid>() == id);

        var run = await client.PostJsonAsync($"/api/v1/admin/reports/saved/{id}/run");
        run.StatusCode.Should().Be(HttpStatusCode.OK);

        var delete = await client.DeleteAsync($"/api/v1/admin/reports/saved/{id}");
        delete.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }
}

public class ExportsApiTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public ExportsApiTests(ApiFactory factory) => _factory = factory;

    [Fact]
    public async Task RequestExport_ThenGet_ReturnsTheQueuedJob()
    {
        var client = _factory.ClientFor(TestTokens.Admin(Guid.NewGuid()));

        var requested = await client.PostJsonAsync("/api/v1/admin/reports/exports", new { refKind = "LaborMarket", refId = (Guid?)null, parameters = new Dictionary<string, string>(), format = "Csv" });
        requested.StatusCode.Should().Be(HttpStatusCode.Accepted);
        var body = await requested.Json();
        body["status"]!.GetValue<string>().Should().Be("Queued");
        var id = body["id"]!.GetValue<Guid>();

        var get = await client.GetAsync($"/api/v1/admin/reports/exports/{id}");
        get.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task RequestExport_Twice_WithTheSameParameters_ReturnsReused()
    {
        var client = _factory.ClientFor(TestTokens.Admin(Guid.NewGuid()));
        var body = new { refKind = "LaborMarket", refId = (Guid?)null, parameters = new Dictionary<string, string> { ["month"] = "2026-05" }, format = "Pdf" };

        var first = await client.PostJsonAsync("/api/v1/admin/reports/exports", body);
        var second = await client.PostJsonAsync("/api/v1/admin/reports/exports", body);

        (await first.Json())["id"]!.GetValue<Guid>().Should().Be((await second.Json())["id"]!.GetValue<Guid>());
        (await second.Json())["reused"]!.GetValue<bool>().Should().BeTrue();
    }
}

public class AccessRulesApiTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public AccessRulesApiTests(ApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Configure_ThenList_RoundTripsTheRule()
    {
        var client = _factory.ClientFor(TestTokens.Admin());
        var role = Guid.NewGuid().ToString();

        var configure = await client.PutJsonAsync("/api/v1/admin/reports/access-rules", new { role, categories = new[] { "EmploymentStatistics" } });
        configure.StatusCode.Should().Be(HttpStatusCode.OK);

        var list = await client.GetAsync("/api/v1/admin/reports/access-rules");
        (await list.Json()).AsArray().Should().Contain(r => r!["role"]!.GetValue<string>() == role);
    }

    [Fact]
    public async Task Configure_AsNonAdministrator_Returns403()
    {
        var response = await _factory.ClientFor(TestTokens.JobSeeker())
            .PutJsonAsync("/api/v1/admin/reports/access-rules", new { role = Guid.NewGuid().ToString(), categories = Array.Empty<string>() });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}

public class ODataApiTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public ODataApiTests(ApiFactory factory) => _factory = factory;

    [Fact]
    public async Task ServiceDocument_Anonymous_Returns401()
    {
        var response = await _factory.ClientFor(null).GetAsync("/odata/v1");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ServiceDocument_WithServiceToken_Returns200_AndListsTheKnownViews()
    {
        var response = await _factory.ClientFor(TestTokens.Service()).GetAsync("/odata/v1");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var names = (await response.Json())["value"]!.AsArray().Select(v => v!["name"]!.GetValue<string>()).ToList();
        names.Should().Contain(new[] { "ActivityDaily", "JobPostings", "SystemMetrics" });
    }

    [Fact]
    public async Task ServiceDocument_AsAdministrator_Returns200_TheServiceDocumentItselfIsNotCategoryGated()
    {
        // Only the per-view query (GetODataViewQuery) is restricted to the System actor type; the plain service-document
        // listing just requires authentication, same as every other [Authorize] action.
        var response = await _factory.ClientFor(TestTokens.Admin()).GetAsync("/odata/v1");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task View_AsAdministrator_Returns403_TheFeedIsForServicePrincipalsOnly()
    {
        var response = await _factory.ClientFor(TestTokens.Admin()).GetAsync("/odata/v1/ActivityDaily");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task View_ActivityDaily_WithServiceToken_Returns200()
    {
        var response = await _factory.ClientFor(TestTokens.Service()).GetAsync("/odata/v1/ActivityDaily");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Json())["@odata.context"]!.GetValue<string>().Should().Contain("ActivityDaily");
    }

    [Fact]
    public async Task View_UnknownName_ReturnsBadRequest()
    {
        var response = await _factory.ClientFor(TestTokens.Service()).GetAsync("/odata/v1/NotAView");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
