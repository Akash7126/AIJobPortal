using JobPlatform.BuildingBlocks.Infrastructure.Interfaces.Persistence;
using JobPlatform.BuildingBlocks.Infrastructure.Persistence;
using JobPlatform.ExternalIntegration.Infrastructure;
using JobPlatform.ExternalIntegration.Infrastructure.Persistence;
using JobPlatform.SharedKernel.Common.Enums;
using JobPlatform.SharedKernel.IntegrationEvents.AccountIdentity;
using JobPlatform.SharedKernel.Messaging;
using JobPlatform.SharedKernel.Messaging.Interfaces;
using JobPlatform.TestSupport;
using Microsoft.Extensions.DependencyInjection;

namespace JobPlatform.ExternalIntegration.Api.IntegrationTests;

/// <summary>Hosts the real External Integration API in-process (SQLite, in-memory cache and bus, fake clock, inline JWKS).</summary>
public sealed class ApiFactory : ApiTestFactory<Program>
{
    protected override string ConnectionStringName => "External";

    /// <summary>Delivers an integration event exactly like the broker consumer would: persist to the inbox, then process it.</summary>
    public async Task IngestAsync(params IIntegrationEvent[] events)
    {
        using var scope = Services.CreateScope();
        var writer = scope.ServiceProvider.GetRequiredService<IInboxWriter>();
        foreach (var e in events)
        {
            await writer.TryAddAsync(new InboxMessage
            {
                MessageId = e.MessageId,
                ConsumerName = DependencyInjection.ConsumerName,
                Type = e.EventType,
                Version = e.Version,
                Payload = IntegrationJson.Serialize(e),
                ReceivedOnUtc = Clock.GetUtcNow().UtcDateTime,
                NextAttemptUtc = Clock.GetUtcNow().UtcDateTime,
                Status = InboxStatus.Pending
            });
        }

        await ProcessInboxAsync<ExternalIntegrationDbContext>();
    }

    public Task<int> PublishAsync() => PublishOutboxAsync<ExternalIntegrationDbContext>();

    public static AccountApprovedIntegrationEvent PartnerAccountApproved(Guid partnerAccountId, Guid? messageId = null) =>
        new(messageId ?? Guid.NewGuid(), DateTime.UtcNow, Guid.NewGuid(), null, partnerAccountId, Guid.NewGuid(), ActorType.ExternalJobSite, 1);

    public static ApiCredentialCreatedIntegrationEvent PartnerCredentialCreated(Guid accountId, DateTime expiresAtUtc, Guid? messageId = null) =>
        new(messageId ?? Guid.NewGuid(), DateTime.UtcNow, Guid.NewGuid(), null, Guid.NewGuid(), accountId, Guid.NewGuid(), expiresAtUtc, 1);
}
