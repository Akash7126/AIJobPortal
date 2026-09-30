using System.Diagnostics;
using System.Text.Json;
using JobPlatform.BuildingBlocks.Infrastructure.Diagnostics;
using JobPlatform.BuildingBlocks.Infrastructure.Interfaces.Messaging;
using JobPlatform.BuildingBlocks.Infrastructure.Interfaces.Persistence;
using JobPlatform.BuildingBlocks.Infrastructure.Messaging;
using JobPlatform.SharedKernel.Application.Interfaces.Cqrs;
using JobPlatform.SharedKernel.Application.Interfaces.Ports;
using JobPlatform.SharedKernel.Domain.Interfaces;
using JobPlatform.SharedKernel.Messaging;
using JobPlatform.SharedKernel.Messaging.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace JobPlatform.BuildingBlocks.Infrastructure.Persistence;

public enum OutboxStatus : byte
{
    Pending = 0,
    Published = 1,
    DeadLettered = 2
}

/// <summary>messaging.OutboxMessages row (foundation section 9.3): written in the same transaction as the aggregate.</summary>
public sealed class OutboxMessage
{
    public Guid Id { get; set; }
    public string Type { get; set; } = string.Empty;
    public int Version { get; set; }
    public string Exchange { get; set; } = string.Empty;
    public string RoutingKey { get; set; } = string.Empty;
    public string Payload { get; set; } = string.Empty;
    public string Headers { get; set; } = "{}";
    public string AggregateId { get; set; } = string.Empty;
    public long AggregateVersion { get; set; }
    public DateTime OccurredOnUtc { get; set; }
    public OutboxStatus Status { get; set; }
    public int Attempts { get; set; }
    public DateTime NextAttemptUtc { get; set; }
    public DateTime? ProcessedOnUtc { get; set; }
    public string? LastError { get; set; }

    public OutboundMessage ToOutbound() =>
        new(Id, Exchange, RoutingKey, JsonSerializer.Deserialize<Dictionary<string, string>>(Headers) ?? new(), Payload);
}

internal sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("OutboxMessages", BaseDbContext.MessagingSchema);
        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).ValueGeneratedNever();
        builder.Property(m => m.Type).HasMaxLength(200).IsRequired();
        builder.Property(m => m.Exchange).HasMaxLength(200).IsRequired();
        builder.Property(m => m.RoutingKey).HasMaxLength(200).IsRequired();
        builder.Property(m => m.AggregateId).HasMaxLength(64).IsRequired();
        builder.Property(m => m.Payload).IsRequired();
        builder.Property(m => m.Headers).IsRequired();
        builder.Property(m => m.LastError).HasMaxLength(2000);
        builder.Property(m => m.Status).HasConversion<byte>();
        builder.HasIndex(m => new { m.Status, m.NextAttemptUtc }).HasDatabaseName("IX_OutboxMessages_Status_NextAttemptUtc");
    }
}

/// <summary>Domain events captured during SaveChanges, dispatched to in-process handlers after the commit.</summary>
public sealed class DomainEventBuffer
{
    private readonly List<IDomainEvent> _events = new();

    public void Add(IDomainEvent domainEvent) => _events.Add(domainEvent);

    public IReadOnlyCollection<IDomainEvent> Drain()
    {
        var snapshot = _events.ToArray();
        _events.Clear();
        return snapshot;
    }
}

/// <summary>Invokes IDomainEventHandler&lt;T&gt; registrations. Handler failures are logged and do not undo the committed command.</summary>
public sealed class DomainEventDispatcher : IDomainEventDispatcher
{
    private readonly IServiceProvider _services;
    private readonly ILogger<DomainEventDispatcher> _logger;

    public DomainEventDispatcher(IServiceProvider services, ILogger<DomainEventDispatcher> logger)
    {
        _services = services;
        _logger = logger;
    }

    public async Task DispatchAsync(IReadOnlyCollection<IDomainEvent> events, CancellationToken ct = default)
    {
        foreach (var domainEvent in events)
        {
            var handlerType = typeof(IDomainEventHandler<>).MakeGenericType(domainEvent.GetType());
            var method = handlerType.GetMethod(nameof(IDomainEventHandler<IDomainEvent>.Handle))!;
            foreach (var handler in _services.GetServices(handlerType))
            {
                try
                {
                    await (Task)method.Invoke(handler, new object[] { domainEvent, ct })!;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Domain event handler {Handler} failed for {Event}", handler!.GetType().Name, domainEvent.GetType().Name);
                }
            }
        }
    }
}

/// <summary>
/// Turns domain events raised by tracked aggregates into outbox rows inside the same SaveChanges (atomic with the aggregate change),
/// and buffers them for in-process dispatch after commit.
/// </summary>
public sealed class OutboxSaveChangesInterceptor : SaveChangesInterceptor
{
    private readonly IEnumerable<IDomainEventMapper> _mappers;
    private readonly ICorrelationContext _correlation;
    private readonly DomainEventBuffer _buffer;

    public OutboxSaveChangesInterceptor(IEnumerable<IDomainEventMapper> mappers, ICorrelationContext correlation, DomainEventBuffer buffer)
    {
        _mappers = mappers;
        _correlation = correlation;
        _buffer = buffer;
    }

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        Capture(eventData.Context);
        return result;
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        Capture(eventData.Context);
        return ValueTask.FromResult(result);
    }

    private void Capture(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        var aggregates = context.ChangeTracker.Entries<IAggregateRoot>().Select(e => e.Entity).Where(a => a.DomainEvents.Count > 0).ToList();
        foreach (var aggregate in aggregates)
        {
            var events = aggregate.DomainEvents.ToList();
            var firstVersion = aggregate.Version - events.Count + 1;
            for (var i = 0; i < events.Count; i++)
            {
                var domainEvent = events[i];
                var ctx = new DomainEventContext(aggregate.AggregateId, firstVersion + i, _correlation.CorrelationId, _correlation.CausationId);
                var integrationEvent = _mappers.Select(m => m.Map(domainEvent, ctx)).FirstOrDefault(e => e is not null);
                if (integrationEvent is not null)
                {
                    context.Set<OutboxMessage>().Add(ToOutboxMessage(integrationEvent, ctx));
                }

                _buffer.Add(domainEvent);
            }

            aggregate.ClearDomainEvents();
        }
    }

    private static OutboxMessage ToOutboxMessage(IIntegrationEvent e, DomainEventContext ctx)
    {
        var envelope = IntegrationJson.ToEnvelope(e);
        var headers = envelope.ToHeaders().ToDictionary(h => h.Key, h => h.Value);
        if (Activity.Current is { IdFormat: ActivityIdFormat.W3C, Id: { } traceParent })
        {
            // Lets the outbox processor (a background thread, no ambient request) continue the trace of the request that raised the event.
            headers[MessagingHeaders.TraceParent] = traceParent;
        }

        return new OutboxMessage
        {
            Id = e.MessageId,
            Type = e.EventType,
            Version = e.Version,
            Exchange = e.Exchange,
            RoutingKey = e.RoutingKey,
            Payload = envelope.Payload,
            Headers = JsonSerializer.Serialize(headers),
            AggregateId = ctx.AggregateId,
            AggregateVersion = ctx.AggregateVersion,
            OccurredOnUtc = e.OccurredOnUtc,
            Status = OutboxStatus.Pending,
            NextAttemptUtc = e.OccurredOnUtc
        };
    }
}

public sealed class OutboxOptions
{
    public int BatchSize { get; set; } = 20;
    public TimeSpan PollInterval { get; set; } = TimeSpan.FromSeconds(1);
    public int MaxAttempts { get; set; } = 10;
    public TimeSpan Retention { get; set; } = TimeSpan.FromDays(14);
    public bool Enabled { get; set; } = true;
}

/// <summary>Publishes pending outbox rows (at-least-once). SQL Server uses UPDLOCK/READPAST so several instances never publish the same row.</summary>
public sealed class OutboxProcessor<TContext> : BackgroundService where TContext : DbContext
{
    private readonly IServiceScopeFactory _scopes;
    private readonly TimeProvider _clock;
    private readonly ILogger<OutboxProcessor<TContext>> _logger;
    private readonly OutboxOptions _options;
    private DateTime _lastHousekeeping = DateTime.MinValue;

    public OutboxProcessor(IServiceScopeFactory scopes, TimeProvider clock, IOptions<OutboxOptions> options, ILogger<OutboxProcessor<TContext>> logger)
    {
        _scopes = scopes;
        _clock = clock;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var processed = await ProcessBatchAsync(stoppingToken);
                if (processed == 0)
                {
                    await Task.Delay(_options.PollInterval, _clock, stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Outbox processing loop failed; retrying");
                await Task.Delay(_options.PollInterval, _clock, stoppingToken);
            }
        }
    }

    /// <summary>Processes one batch; returns the number of rows handled. Public so tests can drive it deterministically.</summary>
    public async Task<int> ProcessBatchAsync(CancellationToken ct)
    {
        using var scope = _scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TContext>();
        var bus = scope.ServiceProvider.GetRequiredService<IIntegrationEventBus>();
        var now = _clock.GetUtcNow().UtcDateTime;

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var messages = await SelectPendingAsync(db, now, ct);
        foreach (var message in messages)
        {
            var outbound = message.ToOutbound();
            var tags = new KeyValuePair<string, object?>("type", message.Type);
            using var activity = BuildingBlockTelemetry.ActivitySource.StartActivity($"publish {message.Type}", ActivityKind.Producer,
                BuildingBlockTelemetry.ParentOf(outbound.Headers));
            activity?.SetTag("messaging.destination.name", message.Exchange);
            activity?.SetTag("messaging.rabbitmq.destination.routing_key", message.RoutingKey);
            try
            {
                await bus.PublishAsync(outbound, ct);
                message.Status = OutboxStatus.Published;
                message.ProcessedOnUtc = now;
                message.LastError = null;
                BuildingBlockTelemetry.OutboxPublished.Add(1, tags);
                BuildingBlockTelemetry.OutboxLag.Record(Math.Max(0, (now - message.OccurredOnUtc).TotalSeconds), tags);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                message.Attempts++;
                message.LastError = Truncate(ex.Message, 2000);
                activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
                BuildingBlockTelemetry.OutboxFailed.Add(1, tags);
                if (message.Attempts >= _options.MaxAttempts)
                {
                    message.Status = OutboxStatus.DeadLettered;
                    BuildingBlockTelemetry.OutboxDeadLettered.Add(1, tags);
                    _logger.LogCritical(ex, "Outbox message {MessageId} ({Type}) dead-lettered after {Attempts} attempts", message.Id, message.Type, message.Attempts);
                }
                else
                {
                    message.NextAttemptUtc = now + Backoff(message.Attempts);
                    _logger.LogWarning(ex, "Publishing outbox message {MessageId} ({Type}) failed (attempt {Attempts})", message.Id, message.Type, message.Attempts);
                }
            }
        }

        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        await HousekeepAsync(db, now, ct);
        return messages.Count;
    }

    public static TimeSpan Backoff(int attempts) => TimeSpan.FromSeconds(Math.Min(300, Math.Pow(2, Math.Min(attempts, 10))));

    private async Task<List<OutboxMessage>> SelectPendingAsync(TContext db, DateTime now, CancellationToken ct)
    {
        var batch = _options.BatchSize;
        if (db.Database.ProviderName?.Contains("SqlServer", StringComparison.Ordinal) == true)
        {
            var pending = (byte)OutboxStatus.Pending;
            return await db.Set<OutboxMessage>()
                .FromSql($"SELECT TOP ({batch}) * FROM messaging.OutboxMessages WITH (UPDLOCK, READPAST, ROWLOCK) WHERE Status = {pending} AND NextAttemptUtc <= {now} ORDER BY OccurredOnUtc")
                .ToListAsync(ct);
        }

        return await db.Set<OutboxMessage>()
            .Where(m => m.Status == OutboxStatus.Pending && m.NextAttemptUtc <= now)
            .OrderBy(m => m.OccurredOnUtc)
            .Take(batch)
            .ToListAsync(ct);
    }

    private async Task HousekeepAsync(TContext db, DateTime now, CancellationToken ct)
    {
        if (now - _lastHousekeeping < TimeSpan.FromHours(1))
        {
            return;
        }

        _lastHousekeeping = now;
        var cutoff = now - _options.Retention;
        await db.Set<OutboxMessage>().Where(m => m.Status == OutboxStatus.Published && m.ProcessedOnUtc < cutoff).ExecuteDeleteAsync(ct);
        if (db.Model.FindEntityType(typeof(IdempotencyKeyRecord)) is not null)
        {
            await DurableIdempotencyStore<BaseDbContext>.PurgeExpiredAsync(db, now, ct);
        }
    }

    private static string Truncate(string value, int max) => value.Length <= max ? value : value[..max];
}
