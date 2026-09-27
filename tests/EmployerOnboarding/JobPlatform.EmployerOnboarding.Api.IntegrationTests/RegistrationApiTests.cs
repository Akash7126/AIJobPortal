using System.Net;
using JobPlatform.TestSupport;

namespace JobPlatform.EmployerOnboarding.Api.IntegrationTests;

public class RegistrationApiTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public RegistrationApiTests(ApiFactory factory) => _factory = factory;

    private static object Level2() => new
    {
        companyName = "Acme Ltd", companyId = "CO-" + Guid.NewGuid().ToString("N")[..8], registrationNumber = "REG-1",
        website = "https://acme.example", industry = "Software", size = "Small", governorate = "Ramallah", city = "Ramallah", street = (string?)null,
        description = "A software company."
    };

    [Fact]
    [Trait("Story", "US-3.1.4-04")]
    [Trait("AC", "AC-01")]
    public async Task FullApprovalFlow_OpensSubmitsAndApproves_AndPublishesTheEvent()
    {
        var employerId = Guid.NewGuid();
        await _factory.IngestAsync(ApiFactory.EmployerAccountApproved(employerId));
        var employer = _factory.ClientFor(TestTokens.Employer(employerId));

        var get = await employer.GetAsync("/api/v1/employers/me/registration");
        get.StatusCode.Should().Be(HttpStatusCode.OK);
        (await get.Json())["status"]!.GetValue<string>().Should().Be("Pending");

        var submit = await employer.PutJsonAsync("/api/v1/employers/me/registration/level2", Level2());
        submit.StatusCode.Should().Be(HttpStatusCode.OK);
        var registrationId = (await submit.Json())["employerRegistrationId"]!.GetValue<Guid>();

        var admin = _factory.ClientFor(TestTokens.Admin());
        var approve = await admin.PostJsonAsync($"/api/v1/admin/employer-registrations/{registrationId}/approve");
        approve.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var after = await (await employer.GetAsync("/api/v1/employers/me/registration")).Json();
        after["status"]!.GetValue<string>().Should().Be("Approved");

        await _factory.PublishAsync();
        _factory.Bus.Messages.Should().Contain(m => m.RoutingKey == "employer-registration.approved.v1");
    }

    [Fact]
    [Trait("Story", "US-3.1.4-04")]
    [Trait("AC", "AC-02")]
    public async Task Approve_Twice_Is409()
    {
        var employerId = Guid.NewGuid();
        await _factory.IngestAsync(ApiFactory.EmployerAccountApproved(employerId));
        var employer = _factory.ClientFor(TestTokens.Employer(employerId));
        await employer.PutJsonAsync("/api/v1/employers/me/registration/level2", Level2());
        var registrationId = (await (await employer.GetAsync("/api/v1/employers/me/registration")).Json())["employerRegistrationId"]!.GetValue<Guid>();
        var admin = _factory.ClientFor(TestTokens.Admin());
        await admin.PostJsonAsync($"/api/v1/admin/employer-registrations/{registrationId}/approve");

        var second = await admin.PostJsonAsync($"/api/v1/admin/employer-registrations/{registrationId}/approve");

        await second.ShouldBeProblemAsync(HttpStatusCode.Conflict, "E-AUM-STATE-APPROVED");
    }

    [Fact]
    [Trait("Story", "US-3.1.4-04")]
    [Trait("AC", "AC-04")]
    public async Task Approve_WithoutSubmittedLevel2_Is422()
    {
        var employerId = Guid.NewGuid();
        await _factory.IngestAsync(ApiFactory.EmployerAccountApproved(employerId));
        var registrationId = (await (await _factory.ClientFor(TestTokens.Employer(employerId)).GetAsync("/api/v1/employers/me/registration")).Json())
            ["employerRegistrationId"]!.GetValue<Guid>();

        var response = await _factory.ClientFor(TestTokens.Admin()).PostJsonAsync($"/api/v1/admin/employer-registrations/{registrationId}/approve");

        await response.ShouldBeProblemAsync(HttpStatusCode.UnprocessableEntity, "E-AUM-PROFILE-NOT-SUBMITTED");
    }

    [Fact]
    public async Task Approve_ByNonAdministrator_Is403()
    {
        var response = await _factory.ClientFor(TestTokens.Employer()).PostJsonAsync($"/api/v1/admin/employer-registrations/{Guid.NewGuid()}/approve");

        await response.ShouldBeProblemAsync(HttpStatusCode.Forbidden, "E-AUM-FORBIDDEN");
    }

    [Fact]
    public async Task SubmitLevel2_ByJobSeeker_Is403()
    {
        var response = await _factory.ClientFor(TestTokens.JobSeeker()).PutJsonAsync("/api/v1/employers/me/registration/level2", Level2());

        await response.ShouldBeProblemAsync(HttpStatusCode.Forbidden, "E-ERPM-FORBIDDEN");
    }

    [Fact]
    public async Task SubmitLevel2_Anonymous_Is401()
    {
        var response = await _factory.ClientFor(null).PutJsonAsync("/api/v1/employers/me/registration/level2", Level2());

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task SubmitLevel2_InvalidWebsite_Is400()
    {
        var employerId = Guid.NewGuid();
        await _factory.IngestAsync(ApiFactory.EmployerAccountApproved(employerId));

        var response = await _factory.ClientFor(TestTokens.Employer(employerId)).PutJsonAsync("/api/v1/employers/me/registration/level2",
            new { companyName = "Acme", companyId = "CO-1", registrationNumber = "R-1", website = "not-a-url", industry = "Software", size = "Small",
                governorate = "Ramallah", city = "Ramallah", street = (string?)null, description = "d" });

        await response.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "VAL.INVALID_REQUEST");
    }

    [Fact]
    [Trait("Story", "US-3.1.2-04")]
    [Trait("AC", "AC-01")]
    public async Task VerificationEvent_ArrivingBeforeRegistration_StillOpensStandingWithBadge()
    {
        var employerId = Guid.NewGuid();

        await _factory.IngestAsync(ApiFactory.EmployerVerified(employerId));

        var internalClient = _factory.ClientFor(TestTokens.Service());
        var body = await (await internalClient.GetAsync($"/internal/v1/employers/{employerId}/standing")).Json();
        body["verified"]!.GetValue<bool>().Should().BeTrue();
        body["badge"]!.GetValue<string>().Should().Be("Verified Employer");
    }
}
