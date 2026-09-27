using JobPlatform.SharedKernel.Domain;

namespace JobPlatform.JobPosting.Domain;

public sealed record JobPostingCreatedDomainEvent(
    DateTime OccurredOnUtc, Guid JobPostingId, Guid EmployerAccountId, Guid ActorId, JobPostingStatus Status, string TitleEn, string CategoryCode,
    IReadOnlyList<string> Skills, VisibilityScope Visibility, JobSourceType Source, string? Location, decimal? SalaryMin, decimal? SalaryMax)
    : DomainEvent(OccurredOnUtc);

public sealed record JobPostingUpdatedDomainEvent(
    DateTime OccurredOnUtc, Guid JobPostingId, Guid EmployerAccountId, JobPostingStatus FromStatus, JobPostingStatus ToStatus, Guid ActorId,
    IReadOnlyList<string> ChangedFields) : DomainEvent(OccurredOnUtc);

public sealed record JobPostingRenewedDomainEvent(DateTime OccurredOnUtc, Guid JobPostingId, Guid ActorId, DateTime NewDeadlineUtc) : DomainEvent(OccurredOnUtc);

public sealed record JobPostingStatusUpdatedDomainEvent(
    DateTime OccurredOnUtc, Guid JobPostingId, Guid EmployerAccountId, JobPostingStatus FromStatus, JobPostingStatus ToStatus, Guid ActorId, string? Reason)
    : DomainEvent(OccurredOnUtc);

public sealed record FavoriteJobListCreatedDomainEvent(DateTime OccurredOnUtc, Guid FavoriteJobListId, Guid ActorId, Guid JobPostingId) : DomainEvent(OccurredOnUtc);

public sealed record InterestedListEntryCreatedDomainEvent(DateTime OccurredOnUtc, Guid InterestedListEntryId, Guid ActorId, InterestedReferenceType ReferenceType)
    : DomainEvent(OccurredOnUtc);

/// <summary>Proposed (gap G-11, handover Q-06): a new/published posting matches an opted-in saved search.</summary>
public sealed record SavedSearchMatchedDomainEvent(DateTime OccurredOnUtc, Guid SavedSearchId, Guid JobSeekerId, Guid JobPostingId, string JobTitle)
    : DomainEvent(OccurredOnUtc);
