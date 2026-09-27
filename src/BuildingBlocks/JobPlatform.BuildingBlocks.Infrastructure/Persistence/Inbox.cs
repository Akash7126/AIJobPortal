using System.Text.Json;
using System.Diagnostics;
using JobPlatform.BuildingBlocks.Infrastructure.Diagnostics;
using JobPlatform.SharedKernel.Application.Persistence;
using JobPlatform.SharedKernel.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace JobPlatform.BuildingBlocks.Infrastructure.Persistence;

public enum InboxStatus : byte
{
    Pending = 0,
    Processed = 1,
    Failed = 2
}

/// <summary>messaging.InboxMessages row (foundation section 9.4): persist-then-process, unique per (MessageId, ConsumerName).</summary>
public sealed class InboxMessage
{
    public Guid MessageId { get; set; }
    public string ConsumerName { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public int Version { get; set; }
    public string Payload { get; set; } = string.Empty;
    public DateTime ReceivedOnUtc { get; set; }
    public DateTime? ProcessedOnUtc { get; set; }
    public int Attempts { get; set; }
    public DateTime NextAttemptUtc { get; set; }
    public InboxStatus Status { get; set; }
    public string? LastError { get; set; }
}

internal sealed class InboxMessageConfiguration : IEntityTypeConfiguration<InboxMessage>
{
    public void Configure(EntityTypeBuilder<InboxMessage> builder)
    {
        builder.ToTable("InboxMessages", BaseDbContext.MessagingSchema);
        builder.HasKey(m => new { m.MessageId, m.ConsumerName });
        builder.Property(m => m.ConsumerName).HasMaxLength(200);
        builder.Property(m => m.Type).HasMaxLength(200).IsRequired();
        builder.Property(m => m.Payload).IsRequired();
        builder.Property(m => m.LastError).HasMaxLength(2000);
        builder.Property(m => m.Status).HasConversion<byte>();
        builder.HasIndex(m => new { m.Status, m.NextAttemptUtc }).HasDatabaseName("IX_InboxMessages_Status_NextAttemptUtc");
    }
}

public static class MessagingModelBuilderExtensions
{
    /// <summary>Adds the outbox, inbox and idempotency tables (schema "messaging") to a BC model.</summary>
    public static ModelBuilder ApplyMessagingConfiguration(this ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new OutboxMessageConfiguration());
        modelBuilder.ApplyConfiguration(new InboxMessageConfiguration());
        modelBuilder.ApplyConfiguration(new IdempotencyKeyConfiguration());
        return modelBuilder;
    }
}

/// <summary>Maps an incoming message type name to the CLR event and the handler that consumes it.</summary>
public sealed record InboxRegistration(string ConsumerName, string EventType, Type EventClrType, Type HandlerType);

public sealed class InboxHandlerRegistry
{
    private readonly Dictionary<string, InboxRegistration> _registrations = new();

    public IReadOnlyCollection<InboxRegistration> All => _registrations.Values;

    public void Add(InboxRegistration registration) => _registrations[Key(registration.ConsumerName, registration.EventType)] = registration;

    public InboxRegistration? Find(string consumerName, string eventType) =>
        _registrations.GetValueOrDefault(Key(consumerName, eventType));

    private static string Key(string consumer, string type) => $"{consumer}|{type}";
}

public interface IInboxWriter
{
    /// <summary>Stores the message. Returns false when (MessageId, ConsumerName) already exists (duplicate delivery - ack and skip).</summary>
    Task<bool> TryAddAsync(InboxMessage message, CancellationToken ct = default);
}

public sealed class EfInboxWriter<TContext> : IInboxWriter where TContext : DbContext
{
    private readonly IServiceScopeFactory _scopes;

    public EfInboxWriter(IServiceScopeFactory scopes) => _scopes = scopes;

    public async Task<bool> TryAddAsync(InboxMessage message, CancellationToken ct = default)
    {
        using var scope = _scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TContext>();
        var exists = await db.Set<InboxMessage>().AnyAsync(m => m.MessageId == message.MessageId && m.ConsumerName == message.ConsumerName, ct);
        if (exists)
        {
            return false;
        }

        db.Set<InboxMessage>().Add(message);
        try
        {
            await db.SaveChangesAsync(ct);
            return true;
        }
        catch (Exception ex) when (ex is UniqueConstraintViolationException or DbUpdateException)
        {
            return false;
        }
    }
}

public sealed class InboxOptions
{
    public int BatchSize { get; set; } = 20;
    public TimeSpan PollInterval { get; set; } = TimeSpan.FromSeconds(1);
    public int MaxAttempts { get; set; } = 5;
    public bool Enabled { get; set; } = true;
}

/// <summary>Processes persisted inbox rows: resolves the registered handler, runs it in a transaction, marks the row processed. Poison rows become Failed and never block others.</summary>
public sealed class InboxProcessor<TContext> : BackgroundService where TContext : DbContext
{
    private readonly IServiceScopeFactory _scopes;
    private readonly InboxHandlerRegistry _registry;
    private readonly TimeProvider _clock;
    private readonly InboxOptions _options;
    private readonly ILogger<InboxProcessor<TContext>> _logger;

    public InboxProcessor(IServiceScopeFactory scopes, InboxHandlerRegistry registry, TimeProvider clock, IOptions<InboxOptions> options,
        ILogger<InboxProcessor<TContext>> logger)
    {
        _scopes = scopes;
        _registry = registry;
        _clock = clock;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled || _registry.All.Count == 0)
        {
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (await ProcessBatchAsync(stoppingToken) == 0)
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
                _logger.LogError(ex, "Inbox processing loop failed; retrying");
                await Task.Delay(_options.PollInterval, _clock, stoppingToken);
            }
        }
    }

    public async Task<int> ProcessBatchAsync(CancellationToken ct)
    {
        List<(Guid Id, string Consumer)> keys;
        var now = _clock.GetUtcNow().UtcDateTime;
        using (var scope = _scopes.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<TContext>();
            keys = (await db.Set<InboxMessage>()
                    .Where(m => m.Status == InboxStatus.Pending && m.NextAttemptUtc <= now)
                    .OrderBy(m => m.ReceivedOnUtc)
                    .Take(_options.BatchSize)
                    .Select(m => new { m.MessageId, m.ConsumerName })
                    .ToListAsync(ct))
                .Select(m => (m.MessageId, m.ConsumerName)).ToList();
        }

        foreach (var (id, consumer) in keys)
        {
            await ProcessOneAsync(id, consumer, ct);
        }

        return keys.Count;
    }

    private async Task ProcessOneAsync(Guid messageId, string consumer, CancellationToken ct)
    {
        using var scope = _scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TContext>();
        var message = await db.Set<InboxMessage>().FirstAsync(m => m.MessageId == messageId && m.ConsumerName == consumer, ct);
        var now = _clock.GetUtcNow().UtcDateTime;
        try
        {
            var registration = _registry.Find(consumer, message.Type)
                               ?? throw new InvalidOperationException($"No handler registered for {message.Type} / {consumer}.");
            var integrationEvent = JsonSerializer.Deserialize(message.Payload, registration.EventClrType, IntegrationJson.Options)
                                   ?? throw new InvalidOperationException("Empty payload.");
            var handler = scope.ServiceProvider.GetRequiredService(registration.HandlerType);
            var handle = registration.HandlerType.GetMethod("Handle") ?? registration.HandlerType.GetInterfaces()
                .SelectMany(i => i.GetMethods()).First(m => m.Name == "Handle");

            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            await (Task)handle.Invoke(handler, new[] { integrationEvent, ct })!;
            message.Status = InboxStatus.Processed;
            message.ProcessedOnUtc = now;
            message.LastError = null;
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            BuildingBlockTelemetry.InboxProcessed.Add(1, new KeyValuePair<string, object?>("type", message.Type));
            BuildingBlockTelemetry.InboxLag.Record(Math.Max(0, (now - message.ReceivedOnUtc).TotalSeconds), new KeyValuePair<string, object?>("type", message.Type));
        }
        catch (Exception thrown) when (thrown is not OperationCanceledException)
        {
            // Reflection wraps synchronous handler exceptions; report the real cause.
            var ex = thrown is System.Reflection.TargetInvocationException { InnerException: { } inner } ? inner : thrown;
            db.ChangeTracker.Clear();
            var failed = await db.Set<InboxMessage>().FirstAsync(m => m.MessageId == messageId && m.ConsumerName == consumer, ct);
            failed.Attempts++;
            BuildingBlockTelemetry.InboxFailed.Add(1, new KeyValuePair<string, object?>("type", failed.Type));
            failed.LastError = ex.Message.Length > 2000 ? ex.Message[..2000] : ex.Message;
            if (failed.Attempts >= _options.MaxAttempts)
            {
                failed.Status = InboxStatus.Failed;
                _logger.LogCritical(ex, "Inbox message {MessageId} ({Type}) failed permanently", messageId, failed.Type);
            }
            else
            {
                failed.NextAttemptUtc = now + OutboxProcessor<TContext>.Backoff(failed.Attempts);
                _logger.LogWarning(ex, "Inbox message {MessageId} ({Type}) failed (attempt {Attempts})", messageId, failed.Type, failed.Attempts);
            }

            await db.SaveChangesAsync(ct);
        }
    }
}
