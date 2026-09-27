using System.Net;
using JobPlatform.GovernmentIntegration.Application;
using JobPlatform.TestSupport;

namespace JobPlatform.GovernmentIntegration.Api.IntegrationTests;

public class EmployerVerificationsApiTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public EmployerVerificationsApiTests(ApiFactory factory) => _factory = factory;

    private static object ValidSubmission() => new { registrationNumber = "REG-1", vatNumber = "VAT-1", mobileNumber = "+970591234567" };

    [Fact]
    [Trait("Story", "US-3.1.2-03")]
    [Trait("AC", "AC-01")]
    public async Task Submit_WithAutomaticMatch_IsVerifiedAndPublishesTheEvent()
    {
        _factory.Mol.NextEmployerOutcome = SourceCallOutcome.Match;
        var employerId = Guid.NewGuid();
        await _factory.IngestAsync(ApiFactory.EmployerAccountCreated(employerId));
        var employer = _factory.ClientFor(TestTokens.Employer(employerId));

        var submit = await employer.PostJsonAsync("/api/v1/employer-verifications", ValidSubmission());

        submit.StatusCode.Should().Be(HttpStatusCode.Accepted);
        var body = await submit.Json();
        body["state"]!.GetValue<string>().Should().Be("Verified");
        var id = body["employerVerificationId"]!.GetValue<Guid>();

        var get = await employer.GetAsync($"/api/v1/employer-verifications/{id}");
        get.StatusCode.Should().Be(HttpStatusCode.OK);

        await _factory.PublishAsync();
        _factory.Bus.Messages.Should().Contain(m => m.RoutingKey == "employer-verification.approved.v1");
    }

    [Fact]
    [Trait("Story", "US-3.1.2-03")]
    [Trait("AC", "AC-02")]
    public async Task Submit_WithNoAutomaticMatch_IsPendingManualReview_AndListedForAdmin()
    {
        _factory.Mol.NextEmployerOutcome = SourceCallOutcome.NoMatch;
        var employerId = Guid.NewGuid();
        await _factory.IngestAsync(ApiFactory.EmployerAccountCreated(employerId));
        var employer = _factory.ClientFor(TestTokens.Employer(employerId));

        var submit = await employer.PostJsonAsync("/api/v1/employer-verifications", ValidSubmission());
        var body = await submit.Json();
        body["state"]!.GetValue<string>().Should().Be("PendingManualReview");
        var id = body["employerVerificationId"]!.GetValue<Guid>();

        var admin = _factory.ClientFor(TestTokens.Admin());
        var pending = await (await admin.GetAsync("/api/v1/employer-verifications/pending-review")).Json();
        pending["items"]!.AsArray().Should().Contain(i => i!["employerVerificationId"]!.GetValue<Guid>() == id);

        var decide = await admin.PostJsonAsync($"/api/v1/employer-verifications/{id}/manual-decision", new { decision = "Approve" });
        decide.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var after = await (await employer.GetAsync($"/api/v1/employer-verifications/{id}")).Json();
        after["state"]!.GetValue<string>().Should().Be("Verified");
    }

    [Fact]
    [Trait("Story", "US-3.1.2-03")]
    public async Task Submit_ByJobSeeker_Is403()
    {
        var response = await _factory.ClientFor(TestTokens.JobSeeker()).PostJsonAsync("/api/v1/employer-verifications", ValidSubmission());

        await response.ShouldBeProblemAsync(HttpStatusCode.Forbidden, "E-GI-FORBIDDEN");
    }

    [Fact]
    public async Task Submit_Anonymous_Is401()
    {
        var response = await _factory.ClientFor(null).PostJsonAsync("/api/v1/employer-verifications", ValidSubmission());

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    [Trait("Story", "US-3.1.2-03")]
    public async Task Submit_UnknownAccount_Is409AccountNotEmployer()
    {
        var response = await _factory.ClientFor(TestTokens.Employer()).PostJsonAsync("/api/v1/employer-verifications", ValidSubmission());

        await response.ShouldBeProblemAsync(HttpStatusCode.Conflict, "E-GI-ACCOUNT-NOT-EMPLOYER");
    }

    [Fact]
    [Trait("Story", "US-3.1.2-03")]
    public async Task Submit_InvalidMobileNumber_Is400()
    {
        var employerId = Guid.NewGuid();
        await _factory.IngestAsync(ApiFactory.EmployerAccountCreated(employerId));

        var response = await _factory.ClientFor(TestTokens.Employer(employerId)).PostJsonAsync("/api/v1/employer-verifications",
            new { registrationNumber = "REG-1", vatNumber = "VAT-1", mobileNumber = "not-a-number" });

        await response.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "VAL.INVALID_REQUEST");
    }

    [Fact]
    public async Task ListPendingReview_ByNonAdministrator_Is403()
    {
        var response = await _factory.ClientFor(TestTokens.Employer()).GetAsync("/api/v1/employer-verifications/pending-review");

        await response.ShouldBeProblemAsync(HttpStatusCode.Forbidden, "E-GI-FORBIDDEN");
    }
}
