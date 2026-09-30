using System.Text;
using JobPlatform.AccountIdentity.Application.Events;
using JobPlatform.AccountIdentity.Domain.Accounts;
using JobPlatform.AccountIdentity.Domain.ApiCredentials;
using JobPlatform.AccountIdentity.Domain.Common;
using JobPlatform.AccountIdentity.Infrastructure.Caching;
using JobPlatform.AccountIdentity.Infrastructure.Persistence;
using JobPlatform.BuildingBlocks.Infrastructure.Caching;
using JobPlatform.BuildingBlocks.Infrastructure.Http;
using JobPlatform.BuildingBlocks.Infrastructure.Interfaces.Messaging;
using JobPlatform.BuildingBlocks.Infrastructure.Messaging;
using JobPlatform.BuildingBlocks.Infrastructure.Persistence;
using JobPlatform.SharedKernel.Application.Interfaces.Ports;
using JobPlatform.SharedKernel.Application.Persistence;
using JobPlatform.SharedKernel.Application.Ports;
using JobPlatform.SharedKernel.Common.Enums;
using JobPlatform.SharedKernel.Common.ValueObjects;
using JobPlatform.SharedKernel.Messaging;
using JobPlatform.SharedKernel.Messaging.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using RabbitMQ.Client;
using StackExchange.Redis;
using Testcontainers.MsSql;
using Testcontainers.RabbitMq;
using Testcontainers.Redis;

namespace JobPlatform.AccountIdentity.Infrastructure.IntegrationTests;

/// <summary>
/// Tests against real SQL Server, RabbitMQ and Redis (Testcontainers). They are written and compile here, but are skipped automatically
/// when Docker is not installed - they have NOT been executed on the machine that produced this code base.
/// </summary>
public class SqlServerTests
{
    private static readonly PasswordHash Hash = new("h:pw");
    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero));

    private static async Task<MsSqlContainer> StartAsync()
    {
        var container = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();
        await container.StartAsync();
        return container;
    }

    private ServiceProvider Services(string connectionString, InMemoryEventBus bus)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<TimeProvider>(_clock);
        services.AddSingleton<IIntegrationEventBus>(bus);
        services.AddScoped<CorrelationContext>();
        services.AddScoped<ICorrelationContext>(sp => sp.GetRequiredService<CorrelationContext>());
        services.AddScoped<DomainEventBuffer>();
        services.AddSingleton<IDomainEventMapper, AccountIdentityEventMapper>();
        services.AddScoped<OutboxSaveChangesInterceptor>();
        services.AddDbContext<IdentityDbContext>((sp, o) => o
            .UseSqlServer(connectionString, sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", IdentityDbContext.Schema))
            .AddInterceptors(sp.GetRequiredService<OutboxSaveChangesInterceptor>())
            .ReplaceService<IModelCacheKeyFactory, ProviderAwareModelCacheKeyFactory>());
        services.Configure<OutboxOptions>(o => o.BatchSize = 10);
        services.AddSingleton<OutboxProcessor<IdentityDbContext>>();
        return services.BuildServiceProvider();
    }

    private Account NewAccount(ActorType type = ActorType.JobSeeker, string mobile = "+970591111111", string? email = "a@example.com", string? identity = null) =>
        Account.Register(new RegistrationDetails(type, "Test", email is null ? null : Email.Create(email), MobileNumber.Create(mobile),
            identity is null ? null : ExternalIdentityKey.Create(identity)), Hash, _clock);

    [DockerFact]
    [Trait("Category", "Docker")]
    public async Task Migrations_Apply_AndTheDocumentedConstraintsHoldOnRealSqlServer()
    {
        await using var container = await StartAsync();
        var bus = new InMemoryEventBus();
        await using var provider = Services(container.GetConnectionString(), bus);

        using (var scope = provider.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<IdentityDbContext>().Database.MigrateAsync();
        }

        using (var scope = provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
            var tables = await db.Database.SqlQueryRaw<string>(
                "SELECT TABLE_SCHEMA + '.' + TABLE_NAME AS Value FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE = 'BASE TABLE'").ToListAsync();
            tables.Should().Contain(new[]
            {
                "identity.Accounts", "identity.ActivationChallenges", "identity.AccountStatusHistory", "identity.ApiCredentials", "identity.PasswordPolicy",
                "identity.SessionSettings", "identity.Roles", "identity.RolePermissions", "identity.AccountRoles", "identity.PrivacyConsents",
                "identity.AccessLog", "identity.SigningKeys", "messaging.OutboxMessages", "messaging.InboxMessages", "messaging.IdempotencyKeys"
            });

            db.Accounts.Add(NewAccount(mobile: "+970591111111", email: "one@example.com"));
            await db.SaveChangesAsync();
        }

        using (var scope = provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
            db.Accounts.Add(NewAccount(mobile: "+970591111111", email: "two@example.com"));
            var duplicate = () => db.SaveChangesAsync();
            await duplicate.Should().ThrowAsync<UniqueConstraintViolationException>("UQ(ActorType, MobileNumber) is enforced by SQL Server error 2601/2627");
        }

        using (var scope = provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
            db.Accounts.AddRange(NewAccount(mobile: "+970592222222", email: null), NewAccount(mobile: "+970593333333", email: null));
            await db.SaveChangesAsync();
        }
    }

    [DockerFact]
    [Trait("Category", "Docker")]
    public async Task DurableIdempotencyStore_ReservesAtomically_AndReplaysAfterACacheFlush_OnRealSqlServer()
    {
        await using var container = await StartAsync();
        await using var provider = Services(container.GetConnectionString(), new InMemoryEventBus());
        using (var scope = provider.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<IdentityDbContext>().Database.MigrateAsync();
        }

        var scopes = provider.GetRequiredService<IServiceScopeFactory>();
        InMemoryCacheStore Cache() => new(_clock, Options.Create(new CacheOptions()));
        var store = new DurableIdempotencyStore<IdentityDbContext>(scopes, Cache(), _clock);

        (await store.TryBeginAsync("scope", "k", "fp", TimeSpan.FromHours(1))).Should().BeTrue();
        (await store.TryBeginAsync("scope", "k", "fp", TimeSpan.FromHours(1))).Should().BeFalse("the composite primary key rejects the second reservation");
        await store.CompleteAsync("scope", "k", new IdempotencyEntry("fp", true, true, "\"stored\"", null), TimeSpan.FromHours(1));

        var afterCacheFlush = new DurableIdempotencyStore<IdentityDbContext>(scopes, Cache(), _clock);
        (await afterCacheFlush.GetAsync("scope", "k"))!.PayloadJson.Should().Be("\"stored\"");
    }

    [DockerFact]
    [Trait("Category", "Docker")]
    public async Task TheMfaReplayColumn_RoundTripsThroughSqlServer()
    {
        await using var container = await StartAsync();
        await using var provider = Services(container.GetConnectionString(), new InMemoryEventBus());
        using (var scope = provider.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<IdentityDbContext>().Database.MigrateAsync();
        }

        var admin = Account.CreateAdministrator("Admin", Email.Create("admin@example.com"), MobileNumber.Create("+970594444444"), Hash, 1, _clock);
        admin.AttemptMfa((long?)123456789, _clock);
        using (var scope = provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
            db.Accounts.Add(admin);
            await db.SaveChangesAsync();
        }

        using (var scope = provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
            (await db.Accounts.AsNoTracking().SingleAsync(a => a.Id == admin.Id)).MfaLastUsedTimeStep.Should().Be(123456789);
        }
    }

    [DockerFact]
    [Trait("Category", "Docker")]
    public async Task OneActiveCredentialPerPartner_AndRowVersionConflicts_AreEnforcedBySqlServer()
    {
        await using var container = await StartAsync();
        await using var provider = Services(container.GetConnectionString(), new InMemoryEventBus());
        using (var scope = provider.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<IdentityDbContext>().Database.MigrateAsync();
        }

        var partner = NewAccount(ActorType.ExternalJobSite, identity: "partner");
        partner.ApproveByStaff(Actor.AuthorisedStaff(Guid.NewGuid()), _clock);
        using (var scope = provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
            db.Accounts.Add(partner);
            db.ApiCredentials.Add(ApiCredential.Issue(partner, "k1", "h1", new CredentialControls(), null, partner.Id.Value, _clock));
            await db.SaveChangesAsync();
        }

        using (var scope = provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
            db.ApiCredentials.Add(ApiCredential.Issue(partner, "k2", "h2", new CredentialControls(), null, partner.Id.Value, _clock));
            var act = () => db.SaveChangesAsync();
            await act.Should().ThrowAsync<UniqueConstraintViolationException>("filtered unique index on Status = 'Active'");
        }

        using var first = provider.CreateScope();
        using var second = provider.CreateScope();
        var db1 = first.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var db2 = second.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var a = await db1.Accounts.SingleAsync(x => x.Id == partner.Id);
        var b = await db2.Accounts.SingleAsync(x => x.Id == partner.Id);
        a.ResetCredentials(Actor.Administrator(Guid.NewGuid()), _clock);
        await db1.SaveChangesAsync();
        b.ResetCredentials(Actor.Administrator(Guid.NewGuid()), _clock);
        var conflict = () => db2.SaveChangesAsync();
        await conflict.Should().ThrowAsync<ConcurrencyConflictException>("rowversion detects the concurrent update");
    }

    [DockerFact]
    [Trait("Category", "Docker")]
    public async Task Outbox_IsWrittenAtomically_AndTheProcessorPublishesWithUpdlockReadpast()
    {
        await using var container = await StartAsync();
        var bus = new InMemoryEventBus();
        await using var provider = Services(container.GetConnectionString(), bus);
        using (var scope = provider.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<IdentityDbContext>().Database.MigrateAsync();
        }

        var account = NewAccount();
        using (var scope = provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
            db.Accounts.Add(account);
            await db.SaveChangesAsync();
        }

        var processor = provider.GetRequiredService<OutboxProcessor<IdentityDbContext>>();
        (await processor.ProcessBatchAsync(default)).Should().Be(1);
        (await processor.ProcessBatchAsync(default)).Should().Be(0);

        bus.Messages.Should().ContainSingle().Which.Type.Should().Be("AccountCreated");
        using var check = provider.CreateScope();
        (await check.ServiceProvider.GetRequiredService<IdentityDbContext>().Set<OutboxMessage>().SingleAsync()).Status.Should().Be(OutboxStatus.Published);
    }
}

public class RabbitMqTests
{
    private static async Task<RabbitMqContainer> StartAsync()
    {
        var container = new RabbitMqBuilder("rabbitmq:3.13-management").Build();
        await container.StartAsync();
        return container;
    }

    private static ConnectionFactory Factory(RabbitMqContainer container) => new()
    {
        HostName = container.Hostname,
        Port = container.GetMappedPublicPort(5672),
        UserName = "guest",
        Password = "guest"
    };

    [DockerFact]
    [Trait("Category", "Docker")]
    public async Task Publisher_SendsPersistentConfirmedMessagesWithTheDocumentedHeaders_ToTheTopicExchange()
    {
        await using var container = await StartAsync();
        var factory = Factory(container);
        await using var bus = new RabbitMqEventBus(Options.Create(new RabbitMqOptions
        {
            HostName = factory.HostName, Port = factory.Port, UserName = "guest", Password = "guest", Exchange = ExchangeNames.AccountIdentity
        }), NullLogger<RabbitMqEventBus>.Instance);
        (await bus.IsHealthyAsync()).Should().BeTrue();

        await using var connection = await factory.CreateConnectionAsync();
        await using var channel = await connection.CreateChannelAsync();
        await channel.QueueDeclareAsync("test.account-events", durable: false, exclusive: false, autoDelete: true);
        await channel.QueueBindAsync("test.account-events", ExchangeNames.AccountIdentity, "account.*.v1");

        var integrationEvent = new JobPlatform.SharedKernel.IntegrationEvents.AccountIdentity.AccountCreatedIntegrationEvent(
            Guid.NewGuid(), DateTime.UtcNow, Guid.NewGuid(), null, Guid.NewGuid(), Guid.NewGuid(), ActorType.Employer, 1);
        await bus.PublishAsync(OutboundMessage.FromEnvelope(IntegrationJson.ToEnvelope(integrationEvent)));

        var received = await channel.BasicGetAsync("test.account-events", autoAck: true);
        received.Should().NotBeNull();
        received!.RoutingKey.Should().Be("account.created.v1");
        received.BasicProperties.DeliveryMode.Should().Be(DeliveryModes.Persistent);
        received.BasicProperties.MessageId.Should().Be(integrationEvent.MessageId.ToString());
        received.BasicProperties.ContentType.Should().Be("application/json");
        Encoding.UTF8.GetString((byte[])received.BasicProperties.Headers!["type"]!).Should().Be("AccountCreated");
        Encoding.UTF8.GetString((byte[])received.BasicProperties.Headers["producer"]!).Should().Be("account-identity");
        Encoding.UTF8.GetString(received.Body.Span).Should().Contain("\"actorType\":\"Employer\"");
    }

    [DockerFact]
    [Trait("Category", "Docker")]
    public async Task ConsumerTopology_DeclaresTheQuorumQueue_RetryQueueAndDeadLetterQueue()
    {
        await using var container = await StartAsync();
        await using var connection = await Factory(container).CreateConnectionAsync();
        await using var channel = await connection.CreateChannelAsync();

        await RabbitMqTopology.DeclareConsumerAsync(channel, "job-seeker-profile", "account-identity", ExchangeNames.AccountIdentity, new[] { RoutingKeys.AccountApproved });

        var main = await channel.QueueDeclarePassiveAsync("q.job-seeker-profile.from.account-identity");
        await channel.QueueDeclarePassiveAsync("q.job-seeker-profile.from.account-identity.retry.30s");
        await channel.QueueDeclarePassiveAsync("q.job-seeker-profile.from.account-identity.dlq");
        main.MessageCount.Should().Be(0);
        await channel.ExchangeDeclarePassiveAsync(ExchangeNames.DeadLetter);
    }

    [DockerFact]
    [Trait("Category", "Docker")]
    public async Task ConsumerTopology_DeclaresAllThreeRetryTiers_AndAFailedMessageWalksThemToTheDeadLetterQueue()
    {
        await using var container = await StartAsync();
        await using var connection = await Factory(container).CreateConnectionAsync();
        await using var channel = await connection.CreateChannelAsync();
        await RabbitMqTopology.DeclareConsumerAsync(channel, "job-seeker-profile", "account-identity", ExchangeNames.AccountIdentity, new[] { RoutingKeys.AccountApproved });
        var main = RabbitMqTopology.MainQueue("job-seeker-profile", "account-identity");
        foreach (var tier in RabbitMqTopology.RetryTiers)
        {
            await channel.QueueDeclarePassiveAsync(RabbitMqTopology.RetryQueue(main, tier));
        }

        // A message that failed four times has one attempt left: its next failure must land in the DLQ, not in a retry tier.
        var properties = new BasicProperties { Headers = new Dictionary<string, object?> { [RabbitMqTopology.AttemptHeader] = 4 } };
        await RabbitMqRetryRouter.RouteFailureAsync(channel, main, 0, properties, new byte[] { 1 }, NullLogger.Instance, default);
        var dead = await channel.BasicGetAsync(RabbitMqTopology.DeadLetterQueue(main), autoAck: true);
        dead.Should().NotBeNull("the attempt cap was reached");

        // The first failure goes to the 30 s tier (and would return to the main queue after the TTL).
        await RabbitMqRetryRouter.RouteFailureAsync(channel, main, 0, new BasicProperties(), new byte[] { 2 }, NullLogger.Instance, default);
        var retrying = await channel.QueueDeclarePassiveAsync(RabbitMqTopology.RetryQueue(main, RabbitMqTopology.RetryTiers[0]));
        retrying.MessageCount.Should().Be(1);
    }
}

public class RedisTests
{
    private static async Task<RedisContainer> StartAsync()
    {
        var container = new RedisBuilder("redis:7.2-alpine").Build();
        await container.StartAsync();
        return container;
    }

    [DockerFact]
    [Trait("Category", "Docker")]
    public async Task RedisCache_CountersSetsLocksAndTtl_BehaveLikeTheInMemoryFallback()
    {
        await using var container = await StartAsync();
        await using var redis = await ConnectionMultiplexer.ConnectAsync(container.GetConnectionString());
        var cache = new RedisCacheStore(redis, Options.Create(new CacheOptions { KeyPrefix = "test:account-identity" }));

        (await cache.IncrementAsync("c", TimeSpan.FromSeconds(30))).Should().Be(1);
        (await cache.IncrementAsync("c", TimeSpan.FromSeconds(30))).Should().Be(2);
        (await cache.GetTimeToLiveAsync("c")).Should().BeGreaterThan(TimeSpan.FromSeconds(20));
        (await cache.SetIfNotExistsAsync("lock", "a", TimeSpan.FromSeconds(30))).Should().BeTrue();
        (await cache.SetIfNotExistsAsync("lock", "b", TimeSpan.FromSeconds(30))).Should().BeFalse();
        await cache.SetAddAsync("s", "x", TimeSpan.FromSeconds(30));
        await cache.SetAddAsync("s", "y", TimeSpan.FromSeconds(30));
        (await cache.SetMembersAsync("s")).Should().BeEquivalentTo("x", "y");
        await cache.SetAsync("short", "v", TimeSpan.FromSeconds(1));
        await Task.Delay(1500);
        (await cache.GetAsync("short")).Should().BeNull();
        (await redis.GetDatabase().StringGetAsync("test:account-identity:c")).HasValue.Should().BeTrue("keys carry the <env>:<bc> prefix");
    }

    [DockerFact]
    [Trait("Category", "Docker")]
    public async Task RateLimiter_AndSessionStore_WorkOnRealRedis()
    {
        await using var container = await StartAsync();
        await using var redis = await ConnectionMultiplexer.ConnectAsync(container.GetConnectionString());
        var cache = new RedisCacheStore(redis, Options.Create(new CacheOptions { KeyPrefix = "test:account-identity" }));
        var limiter = new CacheRateLimiter(cache);

        for (var i = 0; i < 5; i++)
        {
            (await limiter.HitAsync("registration:ip", 5, TimeSpan.FromMinutes(15))).Allowed.Should().BeTrue();
        }

        (await limiter.HitAsync("registration:ip", 5, TimeSpan.FromMinutes(15))).Allowed.Should().BeFalse("the 6th attempt in the window is blocked");

        var clock = TimeProvider.System;
        var store = new CacheSessionStore(cache, clock);
        var session = JobPlatform.AccountIdentity.Domain.Sessions.UserSession.Create(Guid.NewGuid(), 30, "h", clock);
        await store.CreateAsync(session);
        (await store.GetAsync(session.SessionId))!.AccountId.Should().Be(session.AccountId);
        await store.InvalidateAllForAccountAsync(session.AccountId);
        (await store.GetAsync(session.SessionId))!.Status.Should().Be(JobPlatform.AccountIdentity.Domain.Sessions.SessionStatus.Invalidated);
    }
}
