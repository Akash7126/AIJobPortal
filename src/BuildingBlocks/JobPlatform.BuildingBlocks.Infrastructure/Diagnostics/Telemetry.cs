using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace JobPlatform.BuildingBlocks.Infrastructure.Diagnostics;

/// <summary>
/// Traces and metrics of the shared plumbing (foundation section 12 observability baseline): outbox/inbox lag and outcome, handler duration,
/// cache hit ratio. The host subscribes with OpenTelemetry (<c>AddSource/AddMeter(BuildingBlockTelemetry.Name)</c>); without a listener
/// every call is a no-op.
/// </summary>
public static class BuildingBlockTelemetry
{
    public const string Name = "JobPlatform.BuildingBlocks";

    public static readonly ActivitySource ActivitySource = new(Name);
    public static readonly Meter Meter = new(Name);

    public static readonly Counter<long> OutboxPublished = Meter.CreateCounter<long>("jobplatform.outbox.published", description: "Outbox messages published to the broker");
    public static readonly Counter<long> OutboxFailed = Meter.CreateCounter<long>("jobplatform.outbox.failed", description: "Failed outbox publish attempts");
    public static readonly Counter<long> OutboxDeadLettered = Meter.CreateCounter<long>("jobplatform.outbox.dead_lettered", description: "Outbox messages given up on (alert when > 0)");
    public static readonly Histogram<double> OutboxLag = Meter.CreateHistogram<double>("jobplatform.outbox.lag", "s", "Time between the domain event and its publication (alert above 60 s)");

    public static readonly Counter<long> InboxProcessed = Meter.CreateCounter<long>("jobplatform.inbox.processed", description: "Inbox messages handled");
    public static readonly Counter<long> InboxFailed = Meter.CreateCounter<long>("jobplatform.inbox.failed", description: "Failed inbox handling attempts (alert on permanent failures)");
    public static readonly Histogram<double> InboxLag = Meter.CreateHistogram<double>("jobplatform.inbox.lag", "s", "Time between receipt and handling of an inbox message");

    public static readonly Histogram<double> HandlerDuration = Meter.CreateHistogram<double>("jobplatform.handler.duration", "ms", "Command/query handling time");

    public static readonly Counter<long> CacheHits = Meter.CreateCounter<long>("jobplatform.cache.hits", description: "Cache lookups that found a value");
    public static readonly Counter<long> CacheMisses = Meter.CreateCounter<long>("jobplatform.cache.misses", description: "Cache lookups that found nothing");

    /// <summary>Continues the trace of the request that raised the event: parses the stored W3C <c>traceparent</c>, if any.</summary>
    public static ActivityContext ParentOf(IReadOnlyDictionary<string, string> headers) =>
        headers.TryGetValue(JobPlatform.SharedKernel.Messaging.MessagingHeaders.TraceParent, out var traceParent)
        && ActivityContext.TryParse(traceParent, null, out var context)
            ? context
            : default;
}
