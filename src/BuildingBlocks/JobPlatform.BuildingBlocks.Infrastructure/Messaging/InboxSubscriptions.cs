using System.Runtime.CompilerServices;
using JobPlatform.BuildingBlocks.Infrastructure.DependencyInjection;
using JobPlatform.SharedKernel.Messaging.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace JobPlatform.BuildingBlocks.Infrastructure.Messaging;

/// <summary>Where a consuming BC reads from: one queue per (consumer, source BC) bound to the listed routing keys (foundation 9.2).</summary>
public sealed record InboxSubscription(string ConsumerSlug, string SourceSlug, string SourceExchange, IReadOnlyList<string> RoutingKeys);

/// <summary>Reads the constant routing metadata of an integration event type without an instance (all four members are constants per type).</summary>
public static class IntegrationEventInfo
{
    public sealed record Info(string EventType, string Exchange, string RoutingKey, string Producer);

    public static Info Of<TEvent>() where TEvent : IIntegrationEvent
    {
        var probe = (TEvent)RuntimeHelpers.GetUninitializedObject(typeof(TEvent));
        return new Info(probe.EventType, probe.Exchange, probe.RoutingKey, probe.Producer);
    }
}

/// <summary>Keeps the subscription list mutable while services are registered.</summary>
internal sealed class InboxSubscriptionRegistry
{
    private readonly Dictionary<(string Consumer, string Source), (string Exchange, HashSet<string> Keys)> _items = new();

    public void Add(string consumer, string source, string exchange, string routingKey)
    {
        if (!_items.TryGetValue((consumer, source), out var entry))
        {
            entry = (exchange, new HashSet<string>(StringComparer.Ordinal));
            _items[(consumer, source)] = entry;
        }

        entry.Keys.Add(routingKey);
    }

    public IReadOnlyList<InboxSubscription> All =>
        _items.Select(i => new InboxSubscription(i.Key.Consumer, i.Key.Source, i.Value.Exchange, i.Value.Keys.ToArray())).ToArray();
}

public static class InboxSubscriptionExtensions
{
    /// <summary>
    /// Subscribes an inbox handler for an integration event contract: registers the handler for the inbox processor (by catalogue event type)
    /// and the RabbitMQ queue q.&lt;consumer&gt;.from.&lt;producer&gt; bound to the event's routing key. Contract routing data comes from the event type itself,
    /// so publisher and consumer can never disagree.
    /// </summary>
    public static IServiceCollection AddInboxConsumer<TEvent, THandler>(this IServiceCollection services, string consumerSlug, string? routingKeyOverride = null,
        string? sourceSlugOverride = null)
        where TEvent : IIntegrationEvent
        where THandler : class, IIntegrationEventHandler<TEvent>
    {
        var info = IntegrationEventInfo.Of<TEvent>();
        services.AddInboxHandler<TEvent, THandler>(consumerSlug, info.EventType);

        var registry = GetRegistry(services);
        registry.Add(consumerSlug, sourceSlugOverride ?? info.Producer, info.Exchange, routingKeyOverride ?? info.RoutingKey);
        return services;
    }

    private static InboxSubscriptionRegistry GetRegistry(IServiceCollection services)
    {
        var existing = services.FirstOrDefault(d => d.ServiceType == typeof(InboxSubscriptionRegistry))?.ImplementationInstance as InboxSubscriptionRegistry;
        if (existing is not null)
        {
            return existing;
        }

        var registry = new InboxSubscriptionRegistry();
        services.AddSingleton(registry);
        services.AddSingleton<IReadOnlyList<InboxSubscription>>(_ => registry.All);
        services.AddHostedService<RabbitMqInboxConsumerHost>();
        return registry;
    }
}

/// <summary>Starts one persist-then-ack RabbitMQ consumer per subscription when Messaging:Provider = RabbitMq; a no-op otherwise (in-memory bus).</summary>
internal sealed class RabbitMqInboxConsumerHost : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly IConfiguration _configuration;
    private readonly ILogger<RabbitMqInboxConsumerHost> _logger;
    private readonly List<RabbitMqInboxConsumer> _consumers = new();

    public RabbitMqInboxConsumerHost(IServiceProvider services, IConfiguration configuration, ILogger<RabbitMqInboxConsumerHost> logger)
    {
        _services = services;
        _configuration = configuration;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!string.Equals(_configuration["Messaging:Provider"], "RabbitMq", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        foreach (var subscription in _services.GetRequiredService<IReadOnlyList<InboxSubscription>>())
        {
            // The broker may still be starting: retry in the background instead of failing the host.
            for (var attempt = 1; !stoppingToken.IsCancellationRequested; attempt++)
            {
                try
                {
                    var consumer = ActivatorUtilities.CreateInstance<RabbitMqInboxConsumer>(_services);
                    await consumer.StartAsync(subscription.ConsumerSlug, subscription.SourceSlug, subscription.SourceExchange, subscription.RoutingKeys.ToArray(),
                        stoppingToken);
                    _consumers.Add(consumer);
                    break;
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogWarning(ex, "Could not start consumer {Consumer} <- {Source} (attempt {Attempt}); retrying", subscription.ConsumerSlug,
                        subscription.SourceSlug, attempt);
                    await Task.Delay(TimeSpan.FromSeconds(Math.Min(30, attempt * 2)), stoppingToken);
                }
            }
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await base.StopAsync(cancellationToken);
        foreach (var consumer in _consumers)
        {
            await consumer.DisposeAsync();
        }
    }
}
