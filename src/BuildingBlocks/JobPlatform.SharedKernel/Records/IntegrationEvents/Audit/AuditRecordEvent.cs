using JobPlatform.SharedKernel.Messaging;

namespace JobPlatform.SharedKernel.IntegrationEvents.Audit;

/// <summary>
/// Generic operational audit record (foundation 9.1, BC-07 Q-01 / decision D-011): API outcomes, failed syncs, login failures, delivery detail.
/// Published by any BC on exchange jobplatform.audit.records with routing key &lt;source-bc&gt;.&lt;category&gt;.v1. Details must not contain PII or secrets.
/// </summary>
public sealed record AuditRecordIntegrationEvent(
    Guid MessageId, DateTime OccurredOnUtc, Guid CorrelationId, Guid? CausationId,
    string SourceBc, string Category, Guid? ActorId, string SubjectType, string SubjectId, string? OwnerScope,
    string Action, string Outcome, string? Code, IReadOnlyDictionary<string, string>? Details)
    : IntegrationEvent(MessageId, OccurredOnUtc, CorrelationId, CausationId)
{
    public override string EventType => "AuditRecord";
    public override string Exchange => ExchangeNames.AuditRecords;
    public override string Producer => SourceBc;
    public override string RoutingKey => $"{SourceBc}.{Category}.v1";
}
