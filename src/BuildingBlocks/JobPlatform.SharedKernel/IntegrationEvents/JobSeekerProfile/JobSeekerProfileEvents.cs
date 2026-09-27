using JobPlatform.SharedKernel.Messaging;

namespace JobPlatform.SharedKernel.IntegrationEvents.JobSeekerProfile;

/// <summary>Published language of BC-04 Job Seeker Profile. No file content and no PII (the file is fetched through the signed-URL API).</summary>
public abstract record JobSeekerProfileIntegrationEvent(Guid MessageId, DateTime OccurredOnUtc, Guid CorrelationId, Guid? CausationId, int Version = 1)
    : IntegrationEvent(MessageId, OccurredOnUtc, CorrelationId, CausationId, Version)
{
    public override string Exchange => ExchangeNames.JobSeekerProfile;
    public override string Producer => BoundedContextSlugs.JobSeekerProfile;
}

public sealed record ProfileCreatedIntegrationEvent(
    Guid MessageId, DateTime OccurredOnUtc, Guid CorrelationId, Guid? CausationId,
    Guid ProfileId, Guid ActorId, Guid OwnerAccountId, string? Governorate, long AggregateVersion)
    : JobSeekerProfileIntegrationEvent(MessageId, OccurredOnUtc, CorrelationId, CausationId)
{
    public override string EventType => "ProfileCreated";
    public override string RoutingKey => RoutingKeys.ProfileCreated;
}

public sealed record ProfileUpdatedIntegrationEvent(
    Guid MessageId, DateTime OccurredOnUtc, Guid CorrelationId, Guid? CausationId,
    Guid ProfileId, string FromStatus, string ToStatus, Guid ActorId, IReadOnlyList<string> ChangedSections, int CompletionPercent, long AggregateVersion)
    : JobSeekerProfileIntegrationEvent(MessageId, OccurredOnUtc, CorrelationId, CausationId)
{
    public override string EventType => "ProfileUpdated";
    public override string RoutingKey => RoutingKeys.ProfileUpdated;
}

public sealed record ResumeCreatedIntegrationEvent(
    Guid MessageId, DateTime OccurredOnUtc, Guid CorrelationId, Guid? CausationId,
    Guid ResumeId, Guid ActorId, Guid ProfileId, string Format, long SizeBytes, string Sha256, long AggregateVersion)
    : JobSeekerProfileIntegrationEvent(MessageId, OccurredOnUtc, CorrelationId, CausationId)
{
    public override string EventType => "ResumeCreated";
    public override string RoutingKey => RoutingKeys.ResumeCreated;
}
