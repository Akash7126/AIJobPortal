using System.Text;
using JobPlatform.BuildingBlocks.Infrastructure.Interfaces.Messaging;
using JobPlatform.SharedKernel.Messaging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace JobPlatform.BuildingBlocks.Infrastructure.Messaging;

/// <summary>A message ready for the broker: exchange/routing key resolved, headers and JSON body serialized.</summary>
public sealed record OutboundMessage(Guid MessageId, string Exchange, string RoutingKey, IReadOnlyDictionary<string, string> Headers, string Payload)
{
    public static OutboundMessage FromEnvelope(MessageEnvelope envelope) =>
        new(envelope.MessageId, envelope.Exchange, envelope.RoutingKey, envelope.ToHeaders(), envelope.Payload);

    public string Type => Headers.GetValueOrDefault(MessagingHeaders.Type, string.Empty);
}

/// <summary>Test/local bus: records what would have been published.</summary>
public sealed class InMemoryEventBus : IIntegrationEventBus
{
    private readonly List<OutboundMessage> _messages = new();
    private readonly object _gate = new();

    public IReadOnlyList<OutboundMessage> Messages
    {
        get
        {
            lock (_gate)
            {
                return _messages.ToArray();
            }
        }
    }

    /// <summary>When set, PublishAsync throws - lets tests exercise outbox retry/dead-letter behaviour.</summary>
    public Exception? FailWith { get; set; }

    public Task PublishAsync(OutboundMessage message, CancellationToken ct = default)
    {
        if (FailWith is { } failure)
        {
            throw failure;
        }

        lock (_gate)
        {
            _messages.Add(message);
        }

        return Task.CompletedTask;
    }

    public void Clear()
    {
        lock (_gate)
        {
            _messages.Clear();
        }
    }
}

public sealed class RabbitMqOptions
{
    public string HostName { get; set; } = "localhost";
    public int Port { get; set; } = 5672;
    public string UserName { get; set; } = "guest";
    public string Password { get; set; } = "guest";
    public string VirtualHost { get; set; } = "/";

    /// <summary>Topic exchange this service publishes to (jobplatform.&lt;bc-slug&gt;.events).</summary>
    public string Exchange { get; set; } = string.Empty;

    /// <summary>Other exchanges this service publishes to (e.g. jobplatform.audit.records); declared idempotently with the main one.</summary>
    public List<string> AdditionalExchanges { get; set; } = new();
}

/// <summary>One retry tier: a queue whose messages wait <see cref="Delay"/> and then dead-letter back into the main queue.</summary>
public sealed record RetryTier(string Name, TimeSpan Delay);

/// <summary>What to do with a message whose processing just failed.</summary>
public sealed record RetryDecision(bool DeadLetter, RetryTier? Tier, int NextAttempt);

/// <summary>Idempotent topology declaration (foundation section 9.2). The publisher declares its exchange; each consumer declares its own queues.</summary>
public static class RabbitMqTopology
{
    /// <summary>Header carrying the number of failed processing attempts so far (set by the consumer when it re-routes a message).</summary>
    public const string AttemptHeader = "x-attempt";

    /// <summary>Foundation 9.2: 30 s, then 2 min, then 10 min; the last tier repeats until the attempt cap.</summary>
    public static readonly IReadOnlyList<RetryTier> RetryTiers = new[]
    {
        new RetryTier("30s", TimeSpan.FromSeconds(30)),
        new RetryTier("2m", TimeSpan.FromMinutes(2)),
        new RetryTier("10m", TimeSpan.FromMinutes(10))
    };

    public static readonly string[] RetryTierNames = RetryTiers.Select(t => t.Name).ToArray();

    /// <summary>After this many failed attempts the message goes to the DLQ (foundation default 5).</summary>
    public const int MaxDeliveryAttempts = 5;

    public static string MainQueue(string consumerSlug, string sourceSlug) => $"q.{consumerSlug}.from.{sourceSlug}";

    public static string RetryQueue(string mainQueue, RetryTier tier) => $"{mainQueue}.retry.{tier.Name}";

    public static string DeadLetterQueue(string mainQueue) => $"{mainQueue}.dlq";

    /// <summary>Pure retry policy: <paramref name="failedAttempts"/> counts the attempt that just failed (1 = first failure).</summary>
    public static RetryDecision Decide(int failedAttempts)
    {
        if (failedAttempts >= MaxDeliveryAttempts)
        {
            return new RetryDecision(true, null, failedAttempts);
        }

        var tier = RetryTiers[Math.Min(Math.Max(failedAttempts, 1) - 1, RetryTiers.Count - 1)];
        return new RetryDecision(false, tier, failedAttempts);
    }

    public static async Task DeclarePublisherAsync(IChannel channel, string exchange, CancellationToken ct = default)
    {
        await channel.ExchangeDeclareAsync(exchange, ExchangeType.Topic, durable: true, autoDelete: false, cancellationToken: ct);
        await channel.ExchangeDeclareAsync(ExchangeNames.DeadLetter, ExchangeType.Direct, durable: true, autoDelete: false, cancellationToken: ct);
    }

    /// <summary>
    /// Declares q.&lt;consumer&gt;.from.&lt;source&gt; (quorum, durable), one retry queue per tier that dead-letters back into the main queue
    /// through the default exchange (so other consumers of the source exchange never see a redelivery), and the DLQ.
    /// A message rejected without a decision (consumer crash, nack) falls into the first retry tier.
    /// </summary>
    public static async Task DeclareConsumerAsync(IChannel channel, string consumerSlug, string sourceSlug, string sourceExchange,
        IEnumerable<string> routingKeys, CancellationToken ct = default)
    {
        var queue = MainQueue(consumerSlug, sourceSlug);
        var dlq = DeadLetterQueue(queue);

        await DeclarePublisherAsync(channel, sourceExchange, ct);
        await channel.QueueDeclareAsync(dlq, durable: true, exclusive: false, autoDelete: false,
            arguments: new Dictionary<string, object?> { ["x-queue-type"] = "quorum" }, cancellationToken: ct);
        await channel.QueueBindAsync(dlq, ExchangeNames.DeadLetter, dlq, cancellationToken: ct);

        foreach (var tier in RetryTiers)
        {
            var retryQueue = RetryQueue(queue, tier);
            await channel.QueueDeclareAsync(retryQueue, durable: true, exclusive: false, autoDelete: false, arguments: new Dictionary<string, object?>
            {
                ["x-queue-type"] = "quorum",
                ["x-message-ttl"] = (int)tier.Delay.TotalMilliseconds,
                ["x-dead-letter-exchange"] = string.Empty,
                ["x-dead-letter-routing-key"] = queue
            }, cancellationToken: ct);
            await channel.QueueBindAsync(retryQueue, ExchangeNames.DeadLetter, retryQueue, cancellationToken: ct);
        }

        await channel.QueueDeclareAsync(queue, durable: true, exclusive: false, autoDelete: false, arguments: new Dictionary<string, object?>
        {
            ["x-queue-type"] = "quorum",
            ["x-dead-letter-exchange"] = ExchangeNames.DeadLetter,
            ["x-dead-letter-routing-key"] = RetryQueue(queue, RetryTiers[0])
        }, cancellationToken: ct);
        foreach (var key in routingKeys)
        {
            await channel.QueueBindAsync(queue, sourceExchange, key, cancellationToken: ct);
        }
    }
}

/// <summary>RabbitMQ publisher: persistent messages, publisher confirms on, topology declared on first use.</summary>
public sealed class RabbitMqEventBus : IIntegrationEventBus, IAsyncDisposable
{
    private readonly RabbitMqOptions _options;
    private readonly ILogger<RabbitMqEventBus> _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private IConnection? _connection;
    private IChannel? _channel;

    public RabbitMqEventBus(IOptions<RabbitMqOptions> options, ILogger<RabbitMqEventBus> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task PublishAsync(OutboundMessage message, CancellationToken ct = default)
    {
        var channel = await GetChannelAsync(ct);
        var properties = new BasicProperties
        {
            DeliveryMode = DeliveryModes.Persistent,
            ContentType = MessageEnvelope.ContentType,
            MessageId = message.MessageId.ToString(),
            Type = message.Type,
            CorrelationId = message.Headers.GetValueOrDefault(MessagingHeaders.CorrelationId),
            Headers = message.Headers.ToDictionary(h => h.Key, h => (object?)h.Value)
        };

        // Publisher confirms are tracked by the channel: this await throws when the broker nacks or the connection drops.
        await channel.BasicPublishAsync(message.Exchange, message.RoutingKey, mandatory: false, basicProperties: properties,
            body: Encoding.UTF8.GetBytes(message.Payload), cancellationToken: ct);
    }

    public async Task<bool> IsHealthyAsync(CancellationToken ct = default)
    {
        try
        {
            var channel = await GetChannelAsync(ct);
            return channel.IsOpen;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "RabbitMQ health probe failed");
            return false;
        }
    }

    private async Task<IChannel> GetChannelAsync(CancellationToken ct)
    {
        if (_channel is { IsOpen: true })
        {
            return _channel;
        }

        await _gate.WaitAsync(ct);
        try
        {
            if (_channel is { IsOpen: true })
            {
                return _channel;
            }

            if (_connection is not { IsOpen: true })
            {
                var factory = new ConnectionFactory
                {
                    HostName = _options.HostName,
                    Port = _options.Port,
                    UserName = _options.UserName,
                    Password = _options.Password,
                    VirtualHost = _options.VirtualHost
                };
                _connection = await factory.CreateConnectionAsync(ct);
            }

            _channel = await _connection.CreateChannelAsync(
                new CreateChannelOptions(publisherConfirmationsEnabled: true, publisherConfirmationTrackingEnabled: true), ct);
            if (!string.IsNullOrEmpty(_options.Exchange))
            {
                await RabbitMqTopology.DeclarePublisherAsync(_channel, _options.Exchange, ct);
            }

            foreach (var extra in _options.AdditionalExchanges)
            {
                await RabbitMqTopology.DeclarePublisherAsync(_channel, extra, ct);
            }

            return _channel;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_channel is not null)
        {
            await _channel.DisposeAsync();
        }

        if (_connection is not null)
        {
            await _connection.DisposeAsync();
        }

        _gate.Dispose();
    }
}

/// <summary>
/// Generic persist-then-ack consumer (foundation section 9.4): every delivery is written to the inbox first; a duplicate MessageId is acked and skipped.
/// Not used by BC-03 (it consumes nothing) but shipped so other BCs reuse it.
/// </summary>
public sealed class RabbitMqInboxConsumer : IAsyncDisposable
{
    private readonly RabbitMqOptions _options;
    private readonly Interfaces.Persistence.IInboxWriter _inbox;
    private readonly TimeProvider _clock;
    private readonly ILogger<RabbitMqInboxConsumer> _logger;
    private IConnection? _connection;
    private IChannel? _channel;

    public RabbitMqInboxConsumer(IOptions<RabbitMqOptions> options, Interfaces.Persistence.IInboxWriter inbox, TimeProvider clock, ILogger<RabbitMqInboxConsumer> logger)
    {
        _options = options.Value;
        _inbox = inbox;
        _clock = clock;
        _logger = logger;
    }

    public async Task StartAsync(string consumerSlug, string sourceSlug, string sourceExchange, IReadOnlyCollection<string> routingKeys, CancellationToken ct)
    {
        var factory = new ConnectionFactory
        {
            HostName = _options.HostName, Port = _options.Port, UserName = _options.UserName, Password = _options.Password,
            VirtualHost = _options.VirtualHost
        };
        _connection = await factory.CreateConnectionAsync(ct);
        _channel = await _connection.CreateChannelAsync(cancellationToken: ct);
        await RabbitMqTopology.DeclareConsumerAsync(_channel, consumerSlug, sourceSlug, sourceExchange, routingKeys, ct);
        await _channel.BasicQosAsync(0, 20, false, ct);

        var queue = RabbitMqTopology.MainQueue(consumerSlug, sourceSlug);
        var consumer = new AsyncEventingBasicConsumer(_channel);
        consumer.ReceivedAsync += async (_, delivery) =>
        {
            try
            {
                var headers = delivery.BasicProperties.Headers;
                var messageId = Guid.Parse(Encoding.UTF8.GetString((byte[])headers![MessagingHeaders.MessageId]!));
                var type = Encoding.UTF8.GetString((byte[])headers[MessagingHeaders.Type]!);
                var version = int.Parse(Encoding.UTF8.GetString((byte[])headers[MessagingHeaders.Version]!), System.Globalization.CultureInfo.InvariantCulture);
                var now = _clock.GetUtcNow().UtcDateTime;
                await _inbox.TryAddAsync(new Persistence.InboxMessage
                {
                    MessageId = messageId,
                    ConsumerName = consumerSlug,
                    Type = type,
                    Version = version,
                    Payload = Encoding.UTF8.GetString(delivery.Body.Span),
                    ReceivedOnUtc = now,
                    NextAttemptUtc = now,
                    Status = Persistence.InboxStatus.Pending
                }, ct);
                await _channel.BasicAckAsync(delivery.DeliveryTag, false, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Could not persist incoming message {DeliveryTag}; routing it to the retry/dead-letter path", delivery.DeliveryTag);
                await RabbitMqRetryRouter.RouteFailureAsync(_channel!, queue, delivery.DeliveryTag, delivery.BasicProperties, delivery.Body, _logger, ct);
            }
        };
        await _channel.BasicConsumeAsync(queue, autoAck: false, consumer, ct);
    }

    public async ValueTask DisposeAsync()
    {
        if (_channel is not null)
        {
            await _channel.DisposeAsync();
        }

        if (_connection is not null)
        {
            await _connection.DisposeAsync();
        }
    }
}

/// <summary>
/// Failure path of a consumer (foundation section 9.2): the message is re-published to the retry tier for its attempt number
/// (30 s, 2 min, 10 min, ...) or, once the attempt cap is reached, to the dead-letter queue; the original delivery is acked.
/// </summary>
public static class RabbitMqRetryRouter
{
    public static int AttemptsSoFar(IReadOnlyBasicProperties properties)
    {
        if (properties.Headers is null || !properties.Headers.TryGetValue(RabbitMqTopology.AttemptHeader, out var raw))
        {
            return 0;
        }

        return raw switch
        {
            int i => i,
            long l => (int)l,
            byte[] bytes when int.TryParse(Encoding.UTF8.GetString(bytes), System.Globalization.NumberStyles.Integer,
                System.Globalization.CultureInfo.InvariantCulture, out var parsed) => parsed,
            _ => 0
        };
    }

    public static async Task RouteFailureAsync(IChannel channel, string mainQueue, ulong deliveryTag, IReadOnlyBasicProperties properties,
        ReadOnlyMemory<byte> body, ILogger logger, CancellationToken ct)
    {
        var decision = RabbitMqTopology.Decide(AttemptsSoFar(properties) + 1);
        var target = decision.DeadLetter
            ? RabbitMqTopology.DeadLetterQueue(mainQueue)
            : RabbitMqTopology.RetryQueue(mainQueue, decision.Tier!);

        var headers = properties.Headers is null
            ? new Dictionary<string, object?>()
            : new Dictionary<string, object?>(properties.Headers);
        headers[RabbitMqTopology.AttemptHeader] = decision.NextAttempt;

        try
        {
            var republished = new BasicProperties(properties) { Headers = headers, DeliveryMode = DeliveryModes.Persistent };
            await channel.BasicPublishAsync(ExchangeNames.DeadLetter, target, mandatory: false, basicProperties: republished, body: body, cancellationToken: ct);
        }
        catch (Exception ex)
        {
            // Could not re-route: hand the message back to the broker so it is not lost.
            logger.LogError(ex, "Re-routing a failed message to {Target} failed; requeueing it", target);
            await channel.BasicNackAsync(deliveryTag, false, requeue: true, ct);
            return;
        }

        if (decision.DeadLetter)
        {
            logger.LogCritical("Message dead-lettered to {Target} after {Attempts} failed attempts", target, decision.NextAttempt);
        }

        await channel.BasicAckAsync(deliveryTag, false, ct);
    }
}
