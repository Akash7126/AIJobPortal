using System.Text.Json;
using JobPlatform.BuildingBlocks.Infrastructure.Caching;
using JobPlatform.BuildingBlocks.Infrastructure.DependencyInjection;
using JobPlatform.BuildingBlocks.Infrastructure.Http;
using JobPlatform.BuildingBlocks.Infrastructure.Interfaces.Caching;
using JobPlatform.BuildingBlocks.Infrastructure.Interfaces.Messaging;
using JobPlatform.BuildingBlocks.Infrastructure.Interfaces.Persistence;
using JobPlatform.BuildingBlocks.Infrastructure.Messaging;
using JobPlatform.BuildingBlocks.Infrastructure.Persistence;
using JobPlatform.SharedKernel.Application.Interfaces.Cqrs;
using JobPlatform.SharedKernel.Application.Interfaces.Ports;
using JobPlatform.SharedKernel.Application.Persistence;
using JobPlatform.SharedKernel.Application.Ports;
using JobPlatform.SharedKernel.Application.Results;
using JobPlatform.SharedKernel.Common.Enums;
using JobPlatform.SharedKernel.Domain;
using JobPlatform.SharedKernel.Domain.Interfaces;
using JobPlatform.SharedKernel.Messaging;
using JobPlatform.SharedKernel.Messaging.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;

namespace JobPlatform.BuildingBlocks.Infrastructure.UnitTests;

public class CacheAndRateLimiterTests
{
    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));

    private InMemoryCacheStore Store() => new(_clock, Options.Create(new CacheOptions { KeyPrefix = "test:svc" }));

    [Fact]
    public async Task InMemoryCache_SetGetRemove_AndExpiresByTtl()
    {
        var cache = Store();
        await cache.SetAsync("k", "v", TimeSpan.FromMinutes(1));

        (await cache.GetAsync("k")).Should().Be("v");
        (await cache.GetTimeToLiveAsync("k")).Should().Be(TimeSpan.FromMinutes(1));
        _clock.Advance(TimeSpan.FromSeconds(61));
        (await cache.GetAsync("k")).Should().BeNull();
        (await cache.RemoveAsync("missing")).Should().BeFalse();
        await cache.SetAsync("k2", "v", TimeSpan.FromMinutes(1));
        (await cache.RemoveAsync("k2")).Should().BeTrue();
    }

    [Fact]
    public async Task InMemoryCache_SetIfNotExists_IsAtomicAndHonoursExpiry()
    {
        var cache = Store();

        (await cache.SetIfNotExistsAsync("lock", "a", TimeSpan.FromSeconds(10))).Should().BeTrue();
        (await cache.SetIfNotExistsAsync("lock", "b", TimeSpan.FromSeconds(10))).Should().BeFalse();
        _clock.Advance(TimeSpan.FromSeconds(11));
        (await cache.SetIfNotExistsAsync("lock", "c", TimeSpan.FromSeconds(10))).Should().BeTrue();
        (await cache.GetAsync("lock")).Should().Be("c");
    }

    [Fact]
    public async Task InMemoryCache_Counters_SetsAndRefreshTtl()
    {
        var cache = Store();

        (await cache.IncrementAsync("c", TimeSpan.FromMinutes(1))).Should().Be(1);
        (await cache.IncrementAsync("c", TimeSpan.FromMinutes(1))).Should().Be(2);
        _clock.Advance(TimeSpan.FromMinutes(2));
        (await cache.IncrementAsync("c", TimeSpan.FromMinutes(1))).Should().Be(1, "the window restarted");

        await cache.SetAddAsync("s", "a", TimeSpan.FromMinutes(1));
        await cache.SetAddAsync("s", "b", TimeSpan.FromMinutes(1));
        (await cache.SetMembersAsync("s")).Should().BeEquivalentTo("a", "b");
        await cache.SetRemoveAsync("s", "a");
        (await cache.SetMembersAsync("s")).Should().Equal("b");
        _clock.Advance(TimeSpan.FromMinutes(2));
        (await cache.SetMembersAsync("s")).Should().BeEmpty();

        await cache.SetAsync("t", "v", TimeSpan.FromSeconds(10));
        (await cache.RefreshTtlAsync("t", TimeSpan.FromMinutes(5))).Should().BeTrue();
        (await cache.RefreshTtlAsync("missing", TimeSpan.FromMinutes(5))).Should().BeFalse();
        (await cache.GetTimeToLiveAsync("missing")).Should().BeNull();
    }

    [Fact]
    public async Task JsonHelpers_RoundTripTypedValues()
    {
        var cache = Store();
        await cache.SetJsonAsync("j", new Sample("x", 3, ActorType.Employer), TimeSpan.FromMinutes(1));

        (await cache.GetJsonAsync<Sample>("j")).Should().Be(new Sample("x", 3, ActorType.Employer));
        (await cache.GetAsync("j")).Should().Contain("\"actor\":\"Employer\"");
        (await cache.GetJsonAsync<Sample>("missing")).Should().BeNull();
    }

    private sealed record Sample(string Name, int Count, ActorType Actor);

    [Fact]
    public async Task RateLimiter_AllowsTheConfiguredPermits_ThenBlocksUntilTheWindowPasses()
    {
        var limiter = new CacheRateLimiter(Store());

        for (var i = 1; i <= 5; i++)
        {
            (await limiter.HitAsync("reg:ip", 5, TimeSpan.FromMinutes(15))).Allowed.Should().BeTrue($"attempt {i}");
        }

        var sixth = await limiter.HitAsync("reg:ip", 5, TimeSpan.FromMinutes(15));
        sixth.Allowed.Should().BeFalse();
        sixth.Count.Should().Be(6);
        sixth.RetryAfter.Should().BeGreaterThan(TimeSpan.Zero).And.BeLessThanOrEqualTo(TimeSpan.FromMinutes(15));
        (await limiter.CountAsync("reg:ip")).Should().Be(6);
        (await limiter.HitAsync("reg:other", 5, TimeSpan.FromMinutes(15))).Allowed.Should().BeTrue("counters are per key");

        _clock.Advance(TimeSpan.FromMinutes(15) + TimeSpan.FromSeconds(1));
        (await limiter.HitAsync("reg:ip", 5, TimeSpan.FromMinutes(15))).Allowed.Should().BeTrue();
        await limiter.ResetAsync("reg:ip");
        (await limiter.CountAsync("reg:ip")).Should().Be(0);
    }

    [Fact]
    public async Task IdempotencyStore_ReserveCompleteReplayRelease()
    {
        var store = new CacheIdempotencyStore(Store());

        (await store.TryBeginAsync("scope", "k", "fp", TimeSpan.FromHours(1))).Should().BeTrue();
        (await store.TryBeginAsync("scope", "k", "fp", TimeSpan.FromHours(1))).Should().BeFalse();
        (await store.GetAsync("scope", "k"))!.Completed.Should().BeFalse();
        await store.CompleteAsync("scope", "k", new IdempotencyEntry("fp", true, true, "\"payload\"", null), TimeSpan.FromHours(1));
        var done = await store.GetAsync("scope", "k");
        done!.Completed.Should().BeTrue();
        done.PayloadJson.Should().Be("\"payload\"");
        await store.ReleaseAsync("scope", "k");
        (await store.GetAsync("scope", "k")).Should().BeNull();
    }

    [Fact]
    public void ProviderSelection_IsResolvedFromTheFinalConfiguration()
    {
        IServiceProvider Build(string cache, string messaging)
        {
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Cache:Provider"] = cache, ["Messaging:Provider"] = messaging
            }).Build();
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton<IConfiguration>(configuration);
            services.AddBuildingBlocks(configuration);
            return services.BuildServiceProvider();
        }

        var inMemory = Build("InMemory", "InMemory");
        inMemory.GetRequiredService<ICacheStore>().Should().BeOfType<InMemoryCacheStore>();
        inMemory.GetRequiredService<IIntegrationEventBus>().Should().BeOfType<InMemoryEventBus>();

        var rabbit = Build("InMemory", "RabbitMq");
        rabbit.GetRequiredService<IIntegrationEventBus>().Should().BeOfType<RabbitMqEventBus>();
    }
}

public class EventBusAndHttpTests
{
    [Fact]
    public async Task InMemoryEventBus_RecordsMessages_AndCanSimulateAnOutage()
    {
        var bus = new InMemoryEventBus();
        var message = new OutboundMessage(Guid.NewGuid(), "ex", "rk", new Dictionary<string, string> { ["type"] = "Thing" }, "{}");

        await bus.PublishAsync(message);
        bus.Messages.Should().ContainSingle().Which.Type.Should().Be("Thing");
        bus.FailWith = new InvalidOperationException("down");
        var act = () => bus.PublishAsync(message);
        await act.Should().ThrowAsync<InvalidOperationException>();
        bus.Messages.Should().HaveCount(1);
        bus.Clear();
        bus.Messages.Should().BeEmpty();
    }

    [Fact]
    public void Envelope_ProducesTheDocumentedHeaders()
    {
        var envelope = new MessageEnvelope(Guid.NewGuid(), "AccountCreated", 1, "ex", "account.created.v1", "account-identity", Guid.NewGuid(), Guid.NewGuid(),
            new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), "{}");

        var headers = envelope.ToHeaders();

        headers.Keys.Should().BeEquivalentTo("message-id", "type", "version", "correlation-id", "causation-id", "occurred-on", "producer", "content-type");
        headers["content-type"].Should().Be("application/json");
        headers["occurred-on"].Should().StartWith("2026-01-01T00:00:00");
        OutboundMessage.FromEnvelope(envelope).RoutingKey.Should().Be("account.created.v1");
        new MessageEnvelope(Guid.NewGuid(), "T", 1, "e", "r", "p", Guid.NewGuid(), null, DateTime.UtcNow, "{}").ToHeaders().Should().NotContainKey("causation-id");
    }

    [Fact]
    public async Task CorrelationMiddleware_UsesTheIncomingIdOrCreatesOne_AndEchoesItBack()
    {
        var correlation = new CorrelationContext();
        var middleware = new CorrelationIdMiddleware(_ => Task.CompletedTask, NullLogger<CorrelationIdMiddleware>.Instance);
        var incoming = Guid.NewGuid();
        var context = new DefaultHttpContext();
        context.Request.Headers[CorrelationIdMiddleware.HeaderName] = incoming.ToString();

        await middleware.InvokeAsync(context, correlation);
        await context.Response.StartAsync();

        correlation.CorrelationId.Should().Be(incoming);
        context.Response.Headers[CorrelationIdMiddleware.HeaderName].ToString().Should().Be(incoming.ToString());

        var anonymous = new DefaultHttpContext();
        var second = new CorrelationContext();
        await middleware.InvokeAsync(anonymous, second);
        second.CorrelationId.Should().NotBe(Guid.Empty).And.NotBe(incoming);
    }

    [Theory]
    [InlineData(ErrorType.Validation, 400)]
    [InlineData(ErrorType.Unauthorized, 401)]
    [InlineData(ErrorType.Forbidden, 403)]
    [InlineData(ErrorType.NotFound, 404)]
    [InlineData(ErrorType.Conflict, 409)]
    [InlineData(ErrorType.PreconditionFailed, 412)]
    [InlineData(ErrorType.BusinessRule, 422)]
    [InlineData(ErrorType.TooManyRequests, 429)]
    [InlineData(ErrorType.External, 502)]
    [InlineData(ErrorType.Unexpected, 500)]
    public void ProblemDetails_MapsErrorTypesToTheDocumentedStatusCodes(ErrorType type, int status) =>
        ProblemDetailsMapper.StatusFor(type).Should().Be(status);

    [Fact]
    public void ProblemDetails_CarryCodeTraceIdErrorsAndLocalisedDetail_AndHideInternalDetails()
    {
        var services = new ServiceCollection();
        var localizer = Substitute.For<IErrorMessageLocalizer>();
        localizer.Localize("E-X", "fallback", Language.Ar).Returns("رسالة");
        var user = Substitute.For<ICurrentUser>();
        user.Language.Returns(Language.Ar);
        services.AddSingleton(localizer);
        services.AddSingleton(user);
        var context = new DefaultHttpContext { RequestServices = services.BuildServiceProvider(), TraceIdentifier = "trace-1" };
        var error = Error.Validation(new Dictionary<string, string[]> { ["f"] = new[] { "VAL.F" } }, "E-X", "fallback") with { RuleCode = "AI.A.B" };

        var problem = ProblemDetailsMapper.Create(error, context);

        problem.Status.Should().Be(400);
        problem.Detail.Should().Be("رسالة");
        problem.Extensions["code"].Should().Be("E-X");
        problem.Extensions["traceId"].Should().Be("trace-1");
        problem.Extensions["ruleCode"].Should().Be("AI.A.B");
        problem.Extensions.Should().ContainKey("errors");

        var unexpected = ProblemDetailsMapper.Create(Error.Unexpected("E-UNEXPECTED", "stack trace details"), context);
        unexpected.Status.Should().Be(500);
        unexpected.Detail.Should().NotContain("stack trace");
    }
}

// ---------------------------------------------------------------------- outbox / inbox on a real (SQLite in-memory) EF model

public sealed class TestOrder : AggregateRoot<Guid>
{
    private TestOrder()
    {
    }

    public string Number { get; private set; } = string.Empty;

    public static TestOrder Place(string number, DateTime? at = null)
    {
        var order = new TestOrder { Id = Guid.NewGuid(), Number = number };
        order.Raise(new OrderPlaced(order.Id, at ?? DateTime.UtcNow));
        return order;
    }

    public void Rename(string number)
    {
        Number = number;
        Raise(new OrderRenamed(Id, number, DateTime.UtcNow));
    }
}

public sealed record OrderPlaced(Guid OrderId, DateTime At) : DomainEvent(At);

public sealed record OrderRenamed(Guid OrderId, string Number, DateTime At) : DomainEvent(At);

public sealed record OrderPlacedIntegrationEvent(Guid MessageId, DateTime OccurredOnUtc, Guid CorrelationId, Guid? CausationId, Guid OrderId, long AggregateVersion)
    : IntegrationEvent(MessageId, OccurredOnUtc, CorrelationId, CausationId)
{
    public override string EventType => "OrderPlaced";
    public override string Exchange => "jobplatform.test.events";
    public override string RoutingKey => "order.placed.v1";
    public override string Producer => "test";
}

public sealed class TestMapper : IDomainEventMapper
{
    public IIntegrationEvent? Map(IDomainEvent domainEvent, DomainEventContext context) => domainEvent is OrderPlaced placed
        ? new OrderPlacedIntegrationEvent(Guid.NewGuid(), placed.At, context.CorrelationId, context.CausationId, placed.OrderId, context.AggregateVersion)
        : null;
}

public sealed class TestDbContext : BaseDbContext
{
    public TestDbContext(DbContextOptions<TestDbContext> options) : base(options)
    {
    }

    public DbSet<TestOrder> Orders => Set<TestOrder>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TestOrder>(b =>
        {
            b.ToTable("Orders");
            b.HasKey(o => o.Id);
            b.Property(o => o.Id).ValueGeneratedNever();
            b.ConfigureAggregate(IsSqlite);
            b.Property(o => o.Number).HasMaxLength(20);
            b.HasIndex(o => o.Number).IsUnique();
        });
        base.OnModelCreating(modelBuilder);
    }
}

/// <summary>Per-host state so parallel test classes never share handler behaviour.</summary>
public sealed class HandlerProbe
{
    public List<Guid> Handled { get; } = new();
    public bool Fail { get; set; }
}

public sealed class OrderPlacedHandler : IIntegrationEventHandler<OrderPlacedIntegrationEvent>
{
    private readonly HandlerProbe _probe;

    public OrderPlacedHandler(HandlerProbe probe) => _probe = probe;

    public Task Handle(OrderPlacedIntegrationEvent integrationEvent, CancellationToken ct)
    {
        if (_probe.Fail)
        {
            throw new InvalidOperationException("handler failed");
        }

        _probe.Handled.Add(integrationEvent.OrderId);
        return Task.CompletedTask;
    }
}

public sealed class SqliteHost : IAsyncDisposable
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");

    public FakeTimeProvider Clock { get; } = new(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
    public InMemoryEventBus Bus { get; } = new();
    public ServiceProvider Services { get; }
    public HandlerProbe Probe { get; } = new();

    public SqliteHost()
    {
        _connection.Open();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(Probe);
        services.AddSingleton<TimeProvider>(Clock);
        services.AddSingleton<IIntegrationEventBus>(Bus);
        services.AddScoped<CorrelationContext>();
        services.AddScoped<ICorrelationContext>(sp => sp.GetRequiredService<CorrelationContext>());
        services.AddScoped<DomainEventBuffer>();
        services.AddSingleton<IDomainEventMapper, TestMapper>();
        services.AddScoped<OutboxSaveChangesInterceptor>();
        services.AddDbContext<TestDbContext>((sp, o) => o.UseSqlite(_connection).AddInterceptors(sp.GetRequiredService<OutboxSaveChangesInterceptor>()));
        services.Configure<OutboxOptions>(o => o.MaxAttempts = 3);
        services.Configure<InboxOptions>(o => o.MaxAttempts = 3);
        services.AddSingleton<OutboxProcessor<TestDbContext>>();
        services.AddSingleton<InboxHandlerRegistry>(sp =>
        {
            var registry = new InboxHandlerRegistry();
            registry.Add(new InboxRegistration("consumer-a", "OrderPlaced", typeof(OrderPlacedIntegrationEvent), typeof(OrderPlacedHandler)));
            return registry;
        });
        services.AddScoped<OrderPlacedHandler>();
        services.AddSingleton<InboxProcessor<TestDbContext>>();
        services.AddSingleton<IInboxWriter, EfInboxWriter<TestDbContext>>();
        Services = services.BuildServiceProvider();
        using var scope = Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<TestDbContext>().Database.EnsureCreated();
    }

    public async Task<T> WithDb<T>(Func<TestDbContext, Task<T>> action)
    {
        using var scope = Services.CreateScope();
        return await action(scope.ServiceProvider.GetRequiredService<TestDbContext>());
    }

    public async ValueTask DisposeAsync()
    {
        await Services.DisposeAsync();
        await _connection.DisposeAsync();
    }
}

public class OutboxTests : IAsyncLifetime
{
    private SqliteHost _host = null!;

    public Task InitializeAsync()
    {
        _host = new SqliteHost();
        return Task.CompletedTask;
    }

    public async Task DisposeAsync() => await _host.DisposeAsync();

    [Fact]
    public async Task SaveChanges_WritesTheOutboxRow_AtomicallyWithTheAggregate()
    {
        var correlation = Guid.NewGuid();
        Guid orderId;
        using (var scope = _host.Services.CreateScope())
        {
            scope.ServiceProvider.GetRequiredService<CorrelationContext>().Set(correlation);
            var db = scope.ServiceProvider.GetRequiredService<TestDbContext>();
            var order = TestOrder.Place("A-1");
            orderId = order.Id;
            db.Orders.Add(order);
            await db.SaveChangesAsync();

            order.DomainEvents.Should().BeEmpty("events are cleared once captured");
            scope.ServiceProvider.GetRequiredService<DomainEventBuffer>().Drain().Should().ContainSingle().Which.Should().BeOfType<OrderPlaced>();
        }

        var rows = await _host.WithDb(db => db.Set<OutboxMessage>().AsNoTracking().ToListAsync());
        var row = rows.Should().ContainSingle().Which;
        row.Type.Should().Be("OrderPlaced");
        row.AggregateId.Should().Be(orderId.ToString());
        row.AggregateVersion.Should().Be(1);
        row.Status.Should().Be(OutboxStatus.Pending);
        row.RoutingKey.Should().Be("order.placed.v1");
        JsonDocument.Parse(row.Payload).RootElement.GetProperty("correlationId").GetGuid().Should().Be(correlation);
        (await _host.WithDb(db => db.Orders.CountAsync())).Should().Be(1);
    }

    [Fact]
    public async Task ARolledBackTransaction_LeavesNeitherTheAggregateNorTheOutboxRow()
    {
        using (var scope = _host.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<TestDbContext>();
            await db.BeginTransactionAsync();
            db.Orders.Add(TestOrder.Place("B-1"));
            await db.SaveChangesAsync();
            await db.RollbackTransactionAsync();
        }

        (await _host.WithDb(db => db.Orders.CountAsync())).Should().Be(0);
        (await _host.WithDb(db => db.Set<OutboxMessage>().CountAsync())).Should().Be(0);
    }

    [Fact]
    public async Task EventsWithoutAMapping_ProduceNoOutboxRow_ButAreStillBufferedForInProcessHandlers()
    {
        using var scope = _host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TestDbContext>();
        var order = TestOrder.Place("C-1");
        db.Orders.Add(order);
        await db.SaveChangesAsync();
        order.Rename("C-2");
        await db.SaveChangesAsync();

        (await db.Set<OutboxMessage>().CountAsync()).Should().Be(1, "OrderRenamed has no integration mapping");
        scope.ServiceProvider.GetRequiredService<DomainEventBuffer>().Drain().Should().HaveCount(2);
    }

    [Fact]
    public async Task Processor_PublishesPendingRowsOnceAndMarksThemPublished()
    {
        await Place("D-1");
        var processor = _host.Services.GetRequiredService<OutboxProcessor<TestDbContext>>();

        (await processor.ProcessBatchAsync(default)).Should().Be(1);
        (await processor.ProcessBatchAsync(default)).Should().Be(0, "published rows are not picked again");

        var message = _host.Bus.Messages.Should().ContainSingle().Which;
        message.Exchange.Should().Be("jobplatform.test.events");
        message.Headers["type"].Should().Be("OrderPlaced");
        var row = (await _host.WithDb(db => db.Set<OutboxMessage>().ToListAsync())).Single();
        row.Status.Should().Be(OutboxStatus.Published);
        row.ProcessedOnUtc.Should().Be(_host.Clock.GetUtcNow().UtcDateTime);
    }

    [Fact]
    public async Task Processor_BacksOffOnFailure_RetriesLater_AndDeadLettersAfterMaxAttempts()
    {
        await Place("E-1");
        var processor = _host.Services.GetRequiredService<OutboxProcessor<TestDbContext>>();
        _host.Bus.FailWith = new InvalidOperationException("broker down");

        await processor.ProcessBatchAsync(default);
        var afterFirst = (await _host.WithDb(db => db.Set<OutboxMessage>().ToListAsync())).Single();
        afterFirst.Attempts.Should().Be(1);
        afterFirst.LastError.Should().Contain("broker down");
        afterFirst.NextAttemptUtc.Should().Be(_host.Clock.GetUtcNow().UtcDateTime + OutboxProcessor<TestDbContext>.Backoff(1));
        (await processor.ProcessBatchAsync(default)).Should().Be(0, "not due yet");

        _host.Clock.Advance(TimeSpan.FromMinutes(10));
        await processor.ProcessBatchAsync(default);
        _host.Clock.Advance(TimeSpan.FromMinutes(10));
        await processor.ProcessBatchAsync(default);

        var dead = (await _host.WithDb(db => db.Set<OutboxMessage>().ToListAsync())).Single();
        dead.Status.Should().Be(OutboxStatus.DeadLettered);
        dead.Attempts.Should().Be(3);
        _host.Bus.FailWith = null;
        _host.Clock.Advance(TimeSpan.FromMinutes(10));
        (await processor.ProcessBatchAsync(default)).Should().Be(0);
    }

    [Fact]
    public void Backoff_GrowsExponentially_AndIsCapped()
    {
        OutboxProcessor<TestDbContext>.Backoff(1).Should().Be(TimeSpan.FromSeconds(2));
        OutboxProcessor<TestDbContext>.Backoff(3).Should().Be(TimeSpan.FromSeconds(8));
        OutboxProcessor<TestDbContext>.Backoff(20).Should().Be(TimeSpan.FromSeconds(300));
    }

    private async Task Place(string number)
    {
        using var scope = _host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TestDbContext>();
        db.Orders.Add(TestOrder.Place(number, _host.Clock.GetUtcNow().UtcDateTime));
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task UniqueViolations_AreTranslated_AndConcurrencyConflictsToo()
    {
        await Place("F-1");
        using (var scope = _host.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<TestDbContext>();
            db.Orders.Add(TestOrder.Place("F-1"));
            var act = () => db.SaveChangesAsync();
            await act.Should().ThrowAsync<UniqueConstraintViolationException>();
        }

        using var first = _host.Services.CreateScope();
        using var second = _host.Services.CreateScope();
        var db1 = first.ServiceProvider.GetRequiredService<TestDbContext>();
        var db2 = second.ServiceProvider.GetRequiredService<TestDbContext>();
        var a = await db1.Orders.SingleAsync(o => o.Number == "F-1");
        var b = await db2.Orders.SingleAsync(o => o.Number == "F-1");
        a.Rename("F-2");
        await db1.SaveChangesAsync();
        b.Rename("F-3");
        var conflict = () => db2.SaveChangesAsync();
        await conflict.Should().ThrowAsync<ConcurrencyConflictException>("the RowVersion token protects against lost updates");
    }
}

public class InboxTests : IAsyncLifetime
{
    private SqliteHost _host = null!;

    public Task InitializeAsync()
    {
        _host = new SqliteHost();
        return Task.CompletedTask;
    }

    public async Task DisposeAsync() => await _host.DisposeAsync();

    private InboxMessage Message(Guid? id = null, Guid? orderId = null) => new()
    {
        MessageId = id ?? Guid.NewGuid(),
        ConsumerName = "consumer-a",
        Type = "OrderPlaced",
        Version = 1,
        Payload = IntegrationJson.Serialize(new OrderPlacedIntegrationEvent(Guid.NewGuid(), DateTime.UtcNow, Guid.NewGuid(), null, orderId ?? Guid.NewGuid(), 1)),
        ReceivedOnUtc = _host.Clock.GetUtcNow().UtcDateTime,
        NextAttemptUtc = _host.Clock.GetUtcNow().UtcDateTime,
        Status = InboxStatus.Pending
    };

    [Fact]
    public async Task Writer_DedupesByMessageIdAndConsumer()
    {
        var writer = _host.Services.GetRequiredService<IInboxWriter>();
        var id = Guid.NewGuid();

        (await writer.TryAddAsync(Message(id))).Should().BeTrue();
        (await writer.TryAddAsync(Message(id))).Should().BeFalse("a redelivered message is acked and skipped");
        var otherConsumer = Message(id);
        otherConsumer.ConsumerName = "consumer-b";
        (await writer.TryAddAsync(otherConsumer)).Should().BeTrue("the key is (MessageId, ConsumerName)");
        (await _host.WithDb(db => db.Set<InboxMessage>().CountAsync())).Should().Be(2);
    }

    [Fact]
    public async Task Processor_RunsTheRegisteredHandler_AndMarksTheRowProcessed()
    {
        var orderId = Guid.NewGuid();
        await _host.Services.GetRequiredService<IInboxWriter>().TryAddAsync(Message(orderId: orderId));
        var processor = _host.Services.GetRequiredService<InboxProcessor<TestDbContext>>();

        (await processor.ProcessBatchAsync(default)).Should().Be(1);
        (await processor.ProcessBatchAsync(default)).Should().Be(0);

        _host.Probe.Handled.Should().Equal(orderId);
        (await _host.WithDb(db => db.Set<InboxMessage>().SingleAsync())).Status.Should().Be(InboxStatus.Processed);
    }

    [Fact]
    public async Task Processor_RetriesFailures_ThenMarksThePoisonMessageFailedWithoutBlockingOthers()
    {
        var writer = _host.Services.GetRequiredService<IInboxWriter>();
        await writer.TryAddAsync(Message());
        var processor = _host.Services.GetRequiredService<InboxProcessor<TestDbContext>>();
        _host.Probe.Fail = true;

        await processor.ProcessBatchAsync(default);
        var first = await _host.WithDb(db => db.Set<InboxMessage>().SingleAsync());
        first.Attempts.Should().Be(1);
        first.Status.Should().Be(InboxStatus.Pending);
        first.LastError.Should().Contain("handler failed");

        for (var i = 0; i < 2; i++)
        {
            _host.Clock.Advance(TimeSpan.FromMinutes(10));
            await processor.ProcessBatchAsync(default);
        }

        (await _host.WithDb(db => db.Set<InboxMessage>().SingleAsync())).Status.Should().Be(InboxStatus.Failed);

        _host.Probe.Fail = false;
        var goodOrder = Guid.NewGuid();
        await writer.TryAddAsync(Message(orderId: goodOrder));
        await processor.ProcessBatchAsync(default);
        _host.Probe.Handled.Should().Contain(goodOrder);
    }

    [Fact]
    public async Task Processor_UnknownMessageType_IsRetriedThenFailed()
    {
        var unknown = Message();
        unknown.Type = "SomethingElse";
        await _host.Services.GetRequiredService<IInboxWriter>().TryAddAsync(unknown);

        await _host.Services.GetRequiredService<InboxProcessor<TestDbContext>>().ProcessBatchAsync(default);

        (await _host.WithDb(db => db.Set<InboxMessage>().SingleAsync())).LastError.Should().Contain("No handler registered");
    }

    [Fact]
    public async Task DomainEventDispatcher_InvokesHandlers_AndIsolatesTheirFailures()
    {
        var services = new ServiceCollection();
        var good = Substitute.For<IDomainEventHandler<OrderPlaced>>();
        var bad = Substitute.For<IDomainEventHandler<OrderPlaced>>();
        bad.Handle(default!, default).ReturnsForAnyArgs(Task.FromException(new InvalidOperationException("nope")));
        services.AddSingleton(bad);
        services.AddSingleton(good);
        var dispatcher = new DomainEventDispatcher(services.BuildServiceProvider(), NullLogger<DomainEventDispatcher>.Instance);
        var placed = new OrderPlaced(Guid.NewGuid(), DateTime.UtcNow);

        await dispatcher.DispatchAsync(new IDomainEvent[] { placed });

        await good.Received(1).Handle(placed, Arg.Any<CancellationToken>());
        await bad.Received(1).Handle(placed, Arg.Any<CancellationToken>());
    }

    [Fact]
    public void RabbitMqTopology_UsesTheDocumentedNames()
    {
        RabbitMqTopology.MaxDeliveryAttempts.Should().Be(5);
        RabbitMqTopology.RetryTierNames.Should().Contain("30s");
        ExchangeNames.AccountIdentity.Should().Be("jobplatform.account-identity.events");
        ExchangeNames.DeadLetter.Should().Be("jobplatform.dlx");
        RoutingKeys.AccountCreated.Should().Be("account.created.v1");
        RoutingKeys.ApiCredentialCreated.Should().Be("api-credential.created.v1");
    }
}
