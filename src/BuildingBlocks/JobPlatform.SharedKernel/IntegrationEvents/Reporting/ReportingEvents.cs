using JobPlatform.SharedKernel.Messaging;

namespace JobPlatform.SharedKernel.IntegrationEvents.Reporting;

/// <summary>Published language of BC-12 Reporting (proposed events that BC-13 turns into notifications).</summary>
public abstract record ReportingIntegrationEvent(Guid MessageId, DateTime OccurredOnUtc, Guid CorrelationId, Guid? CausationId, int Version = 1)
    : IntegrationEvent(MessageId, OccurredOnUtc, CorrelationId, CausationId, Version)
{
    public override string Exchange => ExchangeNames.Reporting;
    public override string Producer => BoundedContextSlugs.Reporting;
}

public sealed record PerformanceAlertRaisedIntegrationEvent(
    Guid MessageId, DateTime OccurredOnUtc, Guid CorrelationId, Guid? CausationId,
    Guid AlertId, string Metric, string Severity, decimal Value, decimal Threshold, long AggregateVersion)
    : ReportingIntegrationEvent(MessageId, OccurredOnUtc, CorrelationId, CausationId)
{
    public override string EventType => "PerformanceAlertRaised";
    public override string RoutingKey => RoutingKeys.PerformanceAlertRaised;
}

/// <param name="ReportRef">Signed, expiring link to the generated report (never the report content).</param>
public sealed record ReportDistributionRequestedIntegrationEvent(
    Guid MessageId, DateTime OccurredOnUtc, Guid CorrelationId, Guid? CausationId,
    Guid ScheduleId, string ReportRef, IReadOnlyList<string> Recipients, string Format, long AggregateVersion)
    : ReportingIntegrationEvent(MessageId, OccurredOnUtc, CorrelationId, CausationId)
{
    public override string EventType => "ReportDistributionRequested";
    public override string RoutingKey => RoutingKeys.ReportDistributionRequested;
}
