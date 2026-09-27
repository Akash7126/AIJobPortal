using JobPlatform.SharedKernel.Messaging;

namespace JobPlatform.SharedKernel.IntegrationEvents.GovernmentIntegration;

/// <summary>Published language of BC-01 Government Integration. Ids and non-PII fields only (privacy-minimised, A-06-005).</summary>
public abstract record GovernmentIntegrationIntegrationEvent(Guid MessageId, DateTime OccurredOnUtc, Guid CorrelationId, Guid? CausationId, int Version = 1)
    : IntegrationEvent(MessageId, OccurredOnUtc, CorrelationId, CausationId, Version)
{
    public override string Exchange => ExchangeNames.GovernmentIntegration;
    public override string Producer => BoundedContextSlugs.GovernmentIntegration;
}

/// <param name="Method">"Automatic" or "ManualMoL".</param>
public sealed record EmployerVerificationApprovedIntegrationEvent(
    Guid MessageId, DateTime OccurredOnUtc, Guid CorrelationId, Guid? CausationId,
    Guid EmployerVerificationId, Guid EmployerAccountId, Guid ActorId, string Method, long AggregateVersion)
    : GovernmentIntegrationIntegrationEvent(MessageId, OccurredOnUtc, CorrelationId, CausationId)
{
    public override string EventType => "EmployerVerificationApproved";
    public override string RoutingKey => RoutingKeys.EmployerVerificationApproved;
}

/// <param name="Outcome">"Verified" or "NoMatch".</param>
public sealed record GovernmentVerificationDataImportedIntegrationEvent(
    Guid MessageId, DateTime OccurredOnUtc, Guid CorrelationId, Guid? CausationId,
    Guid GovernmentVerificationDataId, string SubjectType, Guid SubjectId, string Outcome, long AggregateVersion)
    : GovernmentIntegrationIntegrationEvent(MessageId, OccurredOnUtc, CorrelationId, CausationId)
{
    public override string EventType => "GovernmentVerificationDataImported";
    public override string RoutingKey => RoutingKeys.GovernmentVerificationDataImported;
}

public sealed record EducationalCredentialVerificationImportedIntegrationEvent(
    Guid MessageId, DateTime OccurredOnUtc, Guid CorrelationId, Guid? CausationId,
    Guid EducationalCredentialVerificationId, Guid SubjectId, string Outcome, long AggregateVersion)
    : GovernmentIntegrationIntegrationEvent(MessageId, OccurredOnUtc, CorrelationId, CausationId)
{
    public override string EventType => "EducationalCredentialVerificationImported";
    public override string RoutingKey => RoutingKeys.EducationalCredentialVerificationImported;
}

public sealed record IdentityVerificationDataImportedIntegrationEvent(
    Guid MessageId, DateTime OccurredOnUtc, Guid CorrelationId, Guid? CausationId,
    Guid IdentityVerificationDataId, Guid SubjectId, string Outcome, long AggregateVersion)
    : GovernmentIntegrationIntegrationEvent(MessageId, OccurredOnUtc, CorrelationId, CausationId)
{
    public override string EventType => "IdentityVerificationDataImported";
    public override string RoutingKey => RoutingKeys.IdentityVerificationDataImported;
}

public sealed record LegacyDataImportedIntegrationEvent(
    Guid MessageId, DateTime OccurredOnUtc, Guid CorrelationId, Guid? CausationId,
    Guid LegacyDataId, Guid MigrationRunId, string RecordType, string SourceSystem, long AggregateVersion)
    : GovernmentIntegrationIntegrationEvent(MessageId, OccurredOnUtc, CorrelationId, CausationId)
{
    public override string EventType => "LegacyDataImported";
    public override string RoutingKey => RoutingKeys.LegacyDataImported;
}

public sealed record DataQualityUpdatedIntegrationEvent(
    Guid MessageId, DateTime OccurredOnUtc, Guid CorrelationId, Guid? CausationId,
    Guid DataQualityId, string FromStatus, string ToStatus, Guid MigrationRunId, int RecordsChecked, int RecordsRejected, long AggregateVersion)
    : GovernmentIntegrationIntegrationEvent(MessageId, OccurredOnUtc, CorrelationId, CausationId)
{
    public override string EventType => "DataQualityUpdated";
    public override string RoutingKey => RoutingKeys.DataQualityUpdated;
}
