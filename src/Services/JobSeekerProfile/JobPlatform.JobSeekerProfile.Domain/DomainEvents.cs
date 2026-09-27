using JobPlatform.SharedKernel.Domain;

namespace JobPlatform.JobSeekerProfile.Domain;

public sealed record ProfileCreatedDomainEvent(Guid ProfileId, Guid OwnerAccountId, Guid ActorId, DateTime OccurredOnUtc) : DomainEvent(OccurredOnUtc);

/// <param name="ChangedSections">Names of the sections touched by this update (e.g. "Level1", "Education", "ExtractedData").</param>
public sealed record ProfileUpdatedDomainEvent(
    Guid ProfileId, string FromStatus, string ToStatus, Guid ActorId, IReadOnlyList<string> ChangedSections, int CompletionPercent, DateTime OccurredOnUtc)
    : DomainEvent(OccurredOnUtc);

public sealed record ResumeCreatedDomainEvent(
    Guid ResumeId, Guid ProfileId, Guid ActorId, string Format, long SizeBytes, string Sha256, DateTime OccurredOnUtc) : DomainEvent(OccurredOnUtc);
