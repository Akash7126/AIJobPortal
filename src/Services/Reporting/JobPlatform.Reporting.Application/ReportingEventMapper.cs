using JobPlatform.Reporting.Domain;
using JobPlatform.SharedKernel.Domain;
using JobPlatform.SharedKernel.IntegrationEvents.Audit;
using JobPlatform.SharedKernel.IntegrationEvents.Reporting;
using JobPlatform.SharedKernel.Messaging;

namespace JobPlatform.Reporting.Application;

/// <summary>
/// Maps the BC's domain events to what other BCs consume (published through the outbox, in the same transaction as the aggregate):
/// PerformanceAlertRaised and ReportDistributionRequested for BC-13 (handover 5.1), and the report access decision as an audit record for BC-07 (AC-03).
/// </summary>
public sealed class ReportingEventMapper : IDomainEventMapper
{
    public IIntegrationEvent? Map(IDomainEvent domainEvent, DomainEventContext context) => domainEvent switch
    {
        PerformanceAlertRaisedDomainEvent e => new PerformanceAlertRaisedIntegrationEvent(Guid.NewGuid(), e.OccurredOnUtc, context.CorrelationId, context.CausationId,
            e.AlertId, e.Metric, e.Severity, e.Value, e.Threshold, context.AggregateVersion),
        ReportDistributionRequestedDomainEvent e => new ReportDistributionRequestedIntegrationEvent(Guid.NewGuid(), e.OccurredOnUtc, context.CorrelationId, context.CausationId,
            e.ScheduleId, e.ReportRef, e.Recipients, e.Format, context.AggregateVersion),
        ReportAccessDecidedDomainEvent e => new AuditRecordIntegrationEvent(Guid.NewGuid(), e.OccurredOnUtc, context.CorrelationId, context.CausationId,
            BoundedContextSlugs.Reporting, "Access", e.ActorId == Guid.Empty ? null : e.ActorId, "ReportCategory", e.Category.ToString(), null, "ReportAccess",
            e.Allowed ? "Success" : "Denied", e.Allowed ? null : CategoryCodes.ForbiddenFor(e.Category),
            new Dictionary<string, string> { ["reportCategory"] = e.Category.ToString(), ["decision"] = e.Allowed ? "Allowed" : "Denied", ["operation"] = e.Request }),
        _ => null
    };
}
