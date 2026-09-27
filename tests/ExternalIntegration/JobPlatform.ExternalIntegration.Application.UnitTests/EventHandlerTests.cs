using JobPlatform.ExternalIntegration.Application.Events;
using JobPlatform.SharedKernel.Common.Enums;
using JobPlatform.SharedKernel.IntegrationEvents.AccountIdentity;
using JobPlatform.SharedKernel.IntegrationEvents.ExternalIntegration;

namespace JobPlatform.ExternalIntegration.Application.UnitTests;

public class EventHandlerTests
{
    [Fact]
    public async Task RegisterKnownPartnerAccountHandler_ForExternalJobSiteActor_RecordsAccount()
    {
        var store = new FakeStore();
        var handler = new RegisterKnownPartnerAccountHandler(store, Kit.Clock());
        var accountId = Guid.NewGuid();
        var evt = new AccountApprovedIntegrationEvent(Guid.NewGuid(), DateTime.UtcNow, Guid.NewGuid(), null, accountId, Guid.NewGuid(),
            ActorType.ExternalJobSite, 1);

        await handler.Handle(evt, CancellationToken.None);

        store.KnownAccounts.Should().ContainSingle(a => a.AccountId == accountId);
    }

    [Fact]
    public async Task RegisterKnownPartnerAccountHandler_ForNonPartnerActor_IsIgnored()
    {
        var store = new FakeStore();
        var handler = new RegisterKnownPartnerAccountHandler(store, Kit.Clock());
        var evt = new AccountApprovedIntegrationEvent(Guid.NewGuid(), DateTime.UtcNow, Guid.NewGuid(), null, Guid.NewGuid(), Guid.NewGuid(),
            ActorType.Employer, 1);

        await handler.Handle(evt, CancellationToken.None);

        store.KnownAccounts.Should().BeEmpty();
    }

    [Fact]
    public async Task RegisterKnownPartnerAccountHandler_Redelivered_DoesNotDuplicate()
    {
        var store = new FakeStore();
        var handler = new RegisterKnownPartnerAccountHandler(store, Kit.Clock());
        var accountId = Guid.NewGuid();
        var evt = new AccountApprovedIntegrationEvent(Guid.NewGuid(), DateTime.UtcNow, Guid.NewGuid(), null, accountId, Guid.NewGuid(),
            ActorType.ExternalJobSite, 1);

        await handler.Handle(evt, CancellationToken.None);
        await handler.Handle(evt with { MessageId = Guid.NewGuid() }, CancellationToken.None);

        store.KnownAccounts.Should().ContainSingle();
    }

    [Fact]
    public async Task RecordPartnerCredentialHandler_NewCredential_IsAdded()
    {
        var store = new FakeStore();
        var handler = new RecordPartnerCredentialHandler(store);
        var credentialId = Guid.NewGuid();
        var evt = new ApiCredentialCreatedIntegrationEvent(Guid.NewGuid(), DateTime.UtcNow, Guid.NewGuid(), null, credentialId, Guid.NewGuid(),
            Guid.NewGuid(), DateTime.UtcNow.AddDays(30), 1);

        await handler.Handle(evt, CancellationToken.None);

        store.Credentials.Should().ContainSingle(c => c.ApiCredentialId == credentialId);
    }

    [Fact]
    [Trait("AC", "out-of-order")]
    public async Task RecordPartnerCredentialHandler_OlderVersion_IsIgnored()
    {
        var store = new FakeStore();
        var handler = new RecordPartnerCredentialHandler(store);
        var credentialId = Guid.NewGuid();
        var accountId = Guid.NewGuid();
        var newer = new ApiCredentialCreatedIntegrationEvent(Guid.NewGuid(), DateTime.UtcNow, Guid.NewGuid(), null, credentialId, accountId,
            Guid.NewGuid(), DateTime.UtcNow.AddDays(30), 3);
        var older = newer with { MessageId = Guid.NewGuid(), AggregateVersion = 1, ExpiresAtUtc = DateTime.UtcNow.AddDays(1) };

        await handler.Handle(newer, CancellationToken.None);
        await handler.Handle(older, CancellationToken.None);

        store.Credentials.Single().ExpiresAtUtc.Should().Be(newer.ExpiresAtUtc);
    }

    [Fact]
    public void EventMapper_MapsEveryPublishedDomainEvent()
    {
        var mapper = new ExternalIntegrationEventMapper();
        var ctx = new SharedKernel.Messaging.DomainEventContext("agg-1", 1, Guid.NewGuid(), null);
        var now = DateTime.UtcNow;

        mapper.Map(new Domain.JobDataImportedDomainEvent(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "p1", "s1", "T", "S",
            new[] { "C#" }, "FullTime", "Remote", null, "Ramallah", null, "Public", false, now), ctx).Should().BeOfType<JobDataImportedIntegrationEvent>();

        mapper.Map(new Domain.JobPostAttributionUpdatedDomainEvent(Guid.NewGuid(), "Active", "Closed", Guid.NewGuid(), "p1", null, null, now), ctx)
            .Should().BeOfType<JobPostAttributionUpdatedIntegrationEvent>();

        mapper.Map(new Domain.JobDataMappingUpdatedDomainEvent(Guid.NewGuid(), "Configured", "Configured", Guid.NewGuid(), Guid.NewGuid(), 2, now), ctx)
            .Should().BeOfType<JobDataMappingUpdatedIntegrationEvent>();

        mapper.Map(new Domain.ExternalJobSiteIntegrationSupportedDomainEvent(Guid.NewGuid(), Guid.NewGuid(), new[] { "pull" }, now), ctx)
            .Should().BeOfType<ExternalJobSiteIntegrationSupportedIntegrationEvent>();

        mapper.Map(new Domain.AttributionVisibilityConfiguredDomainEvent(Guid.NewGuid(), Guid.NewGuid(), "Public", now), ctx)
            .Should().BeOfType<AttributionVisibilityConfiguredIntegrationEvent>();

        mapper.Map(new Domain.ApiSchemaDocumentationViewedDomainEvent(Guid.NewGuid(), Guid.NewGuid(), "v1", now), ctx)
            .Should().BeOfType<ApiSchemaDocumentationViewedIntegrationEvent>();
    }
}
