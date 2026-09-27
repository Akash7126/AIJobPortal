using JobPlatform.SharedKernel.Messaging;

namespace JobPlatform.SharedKernel.IntegrationEvents.JobPosting;

/// <summary>Published language of BC-09 Job Posting. Non-PII dimensions are included so BC-08/10/12/13 need not call back (gap G-17).</summary>
public abstract record JobPostingIntegrationEvent(Guid MessageId, DateTime OccurredOnUtc, Guid CorrelationId, Guid? CausationId, int Version = 1)
    : IntegrationEvent(MessageId, OccurredOnUtc, CorrelationId, CausationId, Version)
{
    public override string Exchange => ExchangeNames.JobPosting;
    public override string Producer => BoundedContextSlugs.JobPosting;
}

/// <param name="Source">"Employer" or "External".</param>
public sealed record JobPostingCreatedIntegrationEvent(
    Guid MessageId, DateTime OccurredOnUtc, Guid CorrelationId, Guid? CausationId,
    Guid JobPostingId, Guid ActorId, Guid EmployerAccountId, string Status, string Title, string Category, IReadOnlyList<string> Skills,
    string Visibility, string Source, string? Location, decimal? SalaryMin, decimal? SalaryMax, long AggregateVersion)
    : JobPostingIntegrationEvent(MessageId, OccurredOnUtc, CorrelationId, CausationId)
{
    public override string EventType => "JobPostingCreated";
    public override string RoutingKey => RoutingKeys.JobPostingCreated;
}

public sealed record JobPostingUpdatedIntegrationEvent(
    Guid MessageId, DateTime OccurredOnUtc, Guid CorrelationId, Guid? CausationId,
    Guid JobPostingId, Guid EmployerAccountId, string FromStatus, string ToStatus, Guid ActorId, IReadOnlyList<string> ChangedFields, long AggregateVersion)
    : JobPostingIntegrationEvent(MessageId, OccurredOnUtc, CorrelationId, CausationId)
{
    public override string EventType => "JobPostingUpdated";
    public override string RoutingKey => RoutingKeys.JobPostingUpdated;
}

public sealed record JobPostingRenewedIntegrationEvent(
    Guid MessageId, DateTime OccurredOnUtc, Guid CorrelationId, Guid? CausationId,
    Guid JobPostingId, Guid ActorId, DateTime NewDeadlineUtc, long AggregateVersion)
    : JobPostingIntegrationEvent(MessageId, OccurredOnUtc, CorrelationId, CausationId)
{
    public override string EventType => "JobPostingRenewed";
    public override string RoutingKey => RoutingKeys.JobPostingRenewed;
}

public sealed record JobPostingStatusUpdatedIntegrationEvent(
    Guid MessageId, DateTime OccurredOnUtc, Guid CorrelationId, Guid? CausationId,
    Guid JobPostingStatusId, Guid JobPostingId, Guid EmployerAccountId, string FromStatus, string ToStatus, Guid ActorId, string? Reason, long AggregateVersion)
    : JobPostingIntegrationEvent(MessageId, OccurredOnUtc, CorrelationId, CausationId)
{
    public override string EventType => "JobPostingStatusUpdated";
    public override string RoutingKey => RoutingKeys.JobPostingStatusUpdated;
}

public sealed record FavoriteJobListCreatedIntegrationEvent(
    Guid MessageId, DateTime OccurredOnUtc, Guid CorrelationId, Guid? CausationId,
    Guid FavoriteJobListId, Guid ActorId, Guid JobPostingId, long AggregateVersion)
    : JobPostingIntegrationEvent(MessageId, OccurredOnUtc, CorrelationId, CausationId)
{
    public override string EventType => "FavoriteJobListCreated";
    public override string RoutingKey => RoutingKeys.FavoriteJobListCreated;
}

public sealed record InterestedListEntryCreatedIntegrationEvent(
    Guid MessageId, DateTime OccurredOnUtc, Guid CorrelationId, Guid? CausationId,
    Guid InterestedListEntryId, Guid ActorId, string ReferenceType, long AggregateVersion)
    : JobPostingIntegrationEvent(MessageId, OccurredOnUtc, CorrelationId, CausationId)
{
    public override string EventType => "InterestedListEntryCreated";
    public override string RoutingKey => RoutingKeys.InterestedListEntryCreated;
}

/// <summary>Proposed (gap G-11): a new posting matches an opted-in saved search of a job seeker.</summary>
public sealed record SavedSearchMatchedIntegrationEvent(
    Guid MessageId, DateTime OccurredOnUtc, Guid CorrelationId, Guid? CausationId,
    Guid SavedSearchId, Guid JobSeekerId, Guid JobPostingId, string JobTitle, long AggregateVersion)
    : JobPostingIntegrationEvent(MessageId, OccurredOnUtc, CorrelationId, CausationId)
{
    public override string EventType => "SavedSearchMatched";
    public override string RoutingKey => RoutingKeys.SavedSearchMatched;
}
