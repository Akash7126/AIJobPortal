using JobPlatform.EmployerOnboarding.Application.DTOs.Standing;
using JobPlatform.EmployerOnboarding.Application.Handlers.Standing;
using JobPlatform.EmployerOnboarding.Application.Queries.Standing;
using JobPlatform.EmployerOnboarding.Domain;
using JobPlatform.SharedKernel.IntegrationEvents.GovernmentIntegration;

namespace JobPlatform.EmployerOnboarding.Application.UnitTests;

public class StandingHandlerTests
{
    [Fact]
    [Trait("Story", "US-3.1.2-04")]
    [Trait("AC", "AC-01")]
    public async Task MarkEmployerVerifiedHandler_SetsFlagAndInvalidatesCache()
    {
        var employerId = Guid.NewGuid();
        var store = new FakeStore();
        store.Standings.Add(EmployerStanding.OpenFor(Guid.NewGuid(), employerId));
        var cache = new FakeEmployerCache();
        cache.Standings[employerId] = new EmployerStandingView(employerId, false, false, null);
        var handler = new MarkEmployerVerifiedHandler(store, cache, Kit.Clock());
        var evt = new EmployerVerificationApprovedIntegrationEvent(Guid.NewGuid(), DateTime.UtcNow, Guid.NewGuid(), null, Guid.NewGuid(), employerId,
            Guid.NewGuid(), "ManualMoL", 1);

        await handler.Handle(evt, CancellationToken.None);

        store.Standings.Single(s => s.EmployerAccountId == employerId).IsVerified.Should().BeTrue();
        cache.Standings.Should().NotContainKey(employerId, "the stale cached view must be evicted");
    }

    [Fact]
    [Trait("Story", "US-3.1.2-04")]
    public async Task MarkEmployerVerifiedHandler_ArrivingBeforeRegistrationExists_StillOpensStanding()
    {
        var employerId = Guid.NewGuid();
        var handler = new MarkEmployerVerifiedHandler(new FakeStore(), new FakeEmployerCache(), Kit.Clock());
        var evt = new EmployerVerificationApprovedIntegrationEvent(Guid.NewGuid(), DateTime.UtcNow, Guid.NewGuid(), null, Guid.NewGuid(), employerId,
            Guid.NewGuid(), "Automatic", 1);

        var act = async () => await handler.Handle(evt, CancellationToken.None);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task GetEmployerStandingHandler_CachesOnMiss()
    {
        var employerId = Guid.NewGuid();
        var readStore = new FakeReadStore { Standing = id => new EmployerStandingView(id, true, true, "Verified Employer") };
        var cache = new FakeEmployerCache();
        var handler = new GetEmployerStandingHandler(cache, readStore);

        var result = await handler.Handle(new GetEmployerStandingQuery(employerId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        cache.Standings.Should().ContainKey(employerId);
    }

    [Fact]
    public async Task GetEmployerStandingHandler_UnknownEmployer_ReturnsNotFound()
    {
        var handler = new GetEmployerStandingHandler(new FakeEmployerCache(), new FakeReadStore());

        var result = await handler.Handle(new GetEmployerStandingQuery(Guid.NewGuid()), CancellationToken.None);

        result.Error!.Code.Should().Be(Domain.Common.ErrorCodes.NotFound);
    }
}
