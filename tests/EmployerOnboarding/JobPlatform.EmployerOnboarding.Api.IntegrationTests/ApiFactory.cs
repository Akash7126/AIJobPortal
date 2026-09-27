using JobPlatform.EmployerOnboarding.Infrastructure;
using JobPlatform.EmployerOnboarding.Infrastructure.Persistence;
using JobPlatform.BuildingBlocks.Infrastructure.Persistence;
using JobPlatform.SharedKernel.Common.Enums;
using JobPlatform.SharedKernel.IntegrationEvents.AccountIdentity;
using JobPlatform.SharedKernel.IntegrationEvents.GovernmentIntegration;
using JobPlatform.SharedKernel.Messaging;
using JobPlatform.TestSupport;
using Microsoft.Extensions.DependencyInjection;

namespace JobPlatform.EmployerOnboarding.Api.IntegrationTests;

/// <summary>Hosts the real Employer Onboarding API in-process (SQLite, in-memory cache and bus, fake clock, inline JWKS).</summary>
public sealed class ApiFactory : ApiTestFactory<Program>
{
    protected override string ConnectionStringName => "EmployerOnboarding";

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

        await ProcessInboxAsync<EmployerOnboardingDbContext>();
    }

    public Task<int> PublishAsync() => PublishOutboxAsync<EmployerOnboardingDbContext>();

    public static AccountApprovedIntegrationEvent EmployerAccountApproved(Guid employerAccountId, Guid? messageId = null) =>
        new(messageId ?? Guid.NewGuid(), DateTime.UtcNow, Guid.NewGuid(), null, employerAccountId, Guid.NewGuid(), ActorType.Employer, 1);

    public static EmployerVerificationApprovedIntegrationEvent EmployerVerified(Guid employerAccountId, long version = 1, Guid? messageId = null) =>
        new(messageId ?? Guid.NewGuid(), DateTime.UtcNow, Guid.NewGuid(), null, Guid.NewGuid(), employerAccountId, Guid.NewGuid(), "ManualMoL", version);
}
