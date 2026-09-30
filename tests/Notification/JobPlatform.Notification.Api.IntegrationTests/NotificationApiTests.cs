using System.Net;
using System.Text;
using JobPlatform.Notification.Domain;
using JobPlatform.Notification.Infrastructure.Persistence;
using JobPlatform.SharedKernel.Common.ValueObjects;
using JobPlatform.TestSupport;
using Microsoft.Extensions.DependencyInjection;

namespace JobPlatform.Notification.Api.IntegrationTests;

public class NotificationsApiTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public NotificationsApiTests(ApiFactory factory) => _factory = factory;

    private Task<Guid> SeedNotificationAsync(Guid recipient, DateTime at) =>
        _factory.WithDbAsync<NotificationDbContext, Guid>(async db =>
        {
            var notification = InAppNotification.Create(recipient, "generic", Categories.Welcome, new LocalizedText("مرحبا", "Welcome"), new LocalizedText("أهلا", "Hello"),
                "/jobs/1", at);
            db.InAppNotifications.Add(notification);
            await db.SaveChangesAsync();
            return notification.Id;
        });

    [Fact]
    public async Task List_Anonymous_Returns401()
    {
        var response = await _factory.ClientFor(null).GetAsync("/api/v1/notifications");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task List_ReturnsOnlyTheCallersOwnNotifications()
    {
        var me = Guid.NewGuid();
        var someoneElse = Guid.NewGuid();
        await SeedNotificationAsync(me, _factory.Clock.GetUtcNow().UtcDateTime);
        await SeedNotificationAsync(someoneElse, _factory.Clock.GetUtcNow().UtcDateTime);

        var response = await _factory.ClientFor(TestTokens.JobSeeker(me)).GetAsync("/api/v1/notifications");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Json();
        var items = body["items"]!["items"]!.AsArray();
        items.Should().ContainSingle();
    }

    [Fact]
    public async Task MarkRead_ThenList_ShowsItAsRead()
    {
        var me = Guid.NewGuid();
        var id = await SeedNotificationAsync(me, _factory.Clock.GetUtcNow().UtcDateTime);
        var client = _factory.ClientFor(TestTokens.JobSeeker(me));

        var read = await client.PostJsonAsync($"/api/v1/notifications/{id}/read");
        read.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var list = await client.GetAsync("/api/v1/notifications");
        var item = (await list.Json())["items"]!["items"]!.AsArray().Single(i => i!["id"]!.GetValue<Guid>() == id);
        item!["status"]!.GetValue<string>().Should().Be("Read");
    }

    [Fact]
    public async Task TakeAction_ReturnsTheActionUrl_AndMarksItRead()
    {
        var me = Guid.NewGuid();
        var id = await SeedNotificationAsync(me, _factory.Clock.GetUtcNow().UtcDateTime);
        var client = _factory.ClientFor(TestTokens.JobSeeker(me));

        var response = await client.PostJsonAsync($"/api/v1/notifications/{id}/action");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Json())["actionUrl"]!.GetValue<string>().Should().Be("/jobs/1");
    }

    [Fact]
    public async Task Delete_ThenList_NoLongerReturnsIt()
    {
        var me = Guid.NewGuid();
        var id = await SeedNotificationAsync(me, _factory.Clock.GetUtcNow().UtcDateTime);
        var client = _factory.ClientFor(TestTokens.JobSeeker(me));

        var delete = await client.DeleteAsync($"/api/v1/notifications/{id}");
        delete.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var afterDelete = await client.PostJsonAsync($"/api/v1/notifications/{id}/read");
        afterDelete.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task MarkRead_SomeoneElsesNotification_IsRefused()
    {
        var owner = Guid.NewGuid();
        var id = await SeedNotificationAsync(owner, _factory.Clock.GetUtcNow().UtcDateTime);
        var intruder = _factory.ClientFor(TestTokens.JobSeeker(Guid.NewGuid()));

        var response = await intruder.PostJsonAsync($"/api/v1/notifications/{id}/read");

        ((int)response.StatusCode).Should().BeOneOf(403, 404);
    }
}

public class PreferencesApiTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public PreferencesApiTests(ApiFactory factory) => _factory = factory;

    [Fact]
    public async Task GetEmailPreferences_Default_AllowsEveryKnownCategory()
    {
        var response = await _factory.ClientFor(TestTokens.JobSeeker()).GetAsync("/api/v1/notification-preferences/email");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var categories = (await response.Json())["categories"]!.AsObject();
        categories[Categories.Welcome]!.GetValue<bool>().Should().BeTrue();
    }

    [Fact]
    public async Task SetEmailPreference_ThenGet_RoundTripsTheChange()
    {
        var client = _factory.ClientFor(TestTokens.JobSeeker());

        var set = await client.PutJsonAsync("/api/v1/notification-preferences/email", new { categories = new Dictionary<string, bool> { [Categories.News] = false }, mode = "Digest" });
        set.StatusCode.Should().Be(HttpStatusCode.OK);

        var get = await client.GetAsync("/api/v1/notification-preferences/email");
        var body = await get.Json();
        body["categories"]![Categories.News]!.GetValue<bool>().Should().BeFalse();
        body["mode"]!.GetValue<string>().Should().Be("Digest");
    }

    [Fact]
    public async Task SetEmailPreference_CannotDisableAMandatoryCategory()
    {
        var client = _factory.ClientFor(TestTokens.JobSeeker());

        var response = await client.PutJsonAsync("/api/v1/notification-preferences/email",
            new { categories = new Dictionary<string, bool> { [Categories.SecurityAlert] = false }, mode = "Immediate" });

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task SetSmsOptIn_WithAnInvalidMobile_Returns400()
    {
        var response = await _factory.ClientFor(TestTokens.JobSeeker()).PutJsonAsync("/api/v1/notification-preferences/sms", new { mobile = "not-a-number", optIn = true });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task SetSmsOptIn_WithAValidMobile_Returns200()
    {
        var response = await _factory.ClientFor(TestTokens.JobSeeker()).PutJsonAsync("/api/v1/notification-preferences/sms", new { mobile = "+970599123456", optIn = true });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Json())["smsOptedIn"]!.GetValue<bool>().Should().BeTrue();
    }
}

public class UnsubscribeApiTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public UnsubscribeApiTests(ApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Get_WithAValidToken_ReturnsTheCategory_Anonymously()
    {
        var accountId = Guid.NewGuid();
        var token = _factory.Services.GetRequiredService<Application.Interfaces.IUnsubscribeTokens>().Create(accountId, Categories.News);

        var response = await _factory.ClientFor(null).GetAsync($"/api/v1/unsubscribe/{Uri.EscapeDataString(token)}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Json())["category"]!.GetValue<string>().Should().Be(Categories.News);
    }

    [Fact]
    public async Task Get_WithAnInvalidToken_Returns400()
    {
        var response = await _factory.ClientFor(null).GetAsync("/api/v1/unsubscribe/not-a-valid-token");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}

public class NotificationAdminApiTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public NotificationAdminApiTests(ApiFactory factory) => _factory = factory;

    [Fact]
    public async Task GetEmailTemplate_AsJobSeeker_Returns403()
    {
        var response = await _factory.ClientFor(TestTokens.JobSeeker()).GetAsync("/api/v1/admin/email-templates/welcome");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetEmailTemplate_AsAdministrator_ReturnsTheSeededWelcomeTemplate()
    {
        var response = await _factory.ClientFor(TestTokens.Admin()).GetAsync("/api/v1/admin/email-templates/welcome");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Json())["code"]!.GetValue<string>().Should().Be("welcome");
    }

    [Fact]
    public async Task PutEmailTemplate_ThenGet_IncrementsTheVersion()
    {
        var client = _factory.ClientFor(TestTokens.Admin());

        var edited = await client.PutJsonAsync("/api/v1/admin/email-templates/welcome", new
        {
            subject = "Welcome aboard {{name}}", body = "Hi {{name}}, glad you're here.", placeholders = new Dictionary<string, string> { ["name"] = "there" }
        });
        edited.StatusCode.Should().Be(HttpStatusCode.OK);
        (await edited.Json())["version"]!.GetValue<int>().Should().BeGreaterThanOrEqualTo(2);

        var get = await client.GetAsync("/api/v1/admin/email-templates/welcome");
        (await get.Json())["subject"]!.GetValue<string>().Should().Be("Welcome aboard {{name}}");
    }

    [Fact]
    public async Task DefineNotificationType_ThenList_RoundTrips()
    {
        var client = _factory.ClientFor(TestTokens.Admin());

        var defined = await client.PutJsonAsync("/api/v1/admin/notification-types",
            new { code = "custom-alert", icon = "warning", colour = "#B91C1C", textAlternative = "Custom alert", isMandatory = false });
        defined.StatusCode.Should().Be(HttpStatusCode.OK);

        var list = await client.GetAsync("/api/v1/admin/notification-types");
        (await list.Json()).AsArray().Should().Contain(t => t!["code"]!.GetValue<string>() == "custom-alert");
    }

    [Fact]
    public async Task EssentialSmsCategories_GetAndPut_AlwaysKeepOtpAndPasswordReset()
    {
        var client = _factory.ClientFor(TestTokens.Admin());

        var get = await client.GetAsync("/api/v1/admin/sms/essential-categories");
        get.StatusCode.Should().Be(HttpStatusCode.OK);

        var rejected = await client.PutJsonAsync("/api/v1/admin/sms/essential-categories", new { categories = new[] { Categories.News } });
        rejected.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var accepted = await client.PutJsonAsync("/api/v1/admin/sms/essential-categories",
            new { categories = new[] { Categories.Otp, Categories.PasswordReset, Categories.SecurityAlert } });
        accepted.StatusCode.Should().Be(HttpStatusCode.OK);
        (await accepted.Json()).AsArray().Select(c => c!.GetValue<string>()).Should().Contain(Categories.SecurityAlert);
    }

    [Fact]
    public async Task ListSmsDeliveries_AsAdministrator_Returns200()
    {
        var response = await _factory.ClientFor(TestTokens.Admin()).GetAsync("/api/v1/admin/sms/deliveries");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}

public class InternalNotificationsApiTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public InternalNotificationsApiTests(ApiFactory factory) => _factory = factory;

    [Fact]
    public async Task SendOtp_WithoutAServiceToken_Returns401()
    {
        var response = await _factory.ClientFor(null).PostJsonAsync("/internal/v1/notifications/otp", new { accountId = Guid.NewGuid(), purpose = "Otp", text = "123456" });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task SendOtp_WithAServiceToken_IsAccepted()
    {
        var response = await _factory.ClientFor(TestTokens.Service())
            .PostJsonAsync("/internal/v1/notifications/otp", new { accountId = Guid.NewGuid(), purpose = "Otp", text = "123456" });

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
        (await response.Json())["accepted"]!.GetValue<bool>().Should().BeTrue();
    }

    [Fact]
    public async Task GetDetail_AsAdministrator_Returns403_TheEndpointIsServiceToServiceOnly()
    {
        var response = await _factory.ClientFor(TestTokens.Admin()).GetAsync($"/internal/v1/notifications/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}

public class WebhooksApiTests : IClassFixture<ApiFactory>
{
    private const string WebhookSecret = "dev-only-webhook-secret-change-me";
    private readonly ApiFactory _factory;

    public WebhooksApiTests(ApiFactory factory) => _factory = factory;

    [Fact]
    public async Task SmsDelivery_WithoutASignature_Returns401()
    {
        var response = await _factory.ClientFor(null).PostAsync("/api/v1/webhooks/sms-delivery",
            new StringContent("{\"providerMessageId\":\"x\",\"status\":\"Delivered\"}", Encoding.UTF8, "application/json"));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task SmsDelivery_WithAValidSignature_ForAnUnknownProviderId_StillReturns204()
    {
        var body = "{\"providerMessageId\":\"unknown-id\",\"status\":\"Delivered\"}";
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/webhooks/sms-delivery") { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        request.Headers.Add("X-Signature", Infrastructure.Adapters.HmacWebhookVerifier.Sign(WebhookSecret, body));

        var response = await _factory.ClientFor(null).SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }
}
