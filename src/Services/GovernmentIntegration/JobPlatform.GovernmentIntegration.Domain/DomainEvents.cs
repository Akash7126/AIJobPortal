using JobPlatform.SharedKernel.Domain;

namespace JobPlatform.GovernmentIntegration.Domain;

/// <param name="Method">"Automatic" or "ManualMoL".</param>
public sealed record EmployerVerificationApprovedDomainEvent(Guid EmployerVerificationId, Guid EmployerAccountId, Guid ActorId, string Method, DateTime At)
    : DomainEvent(At);

/// <param name="Outcome">"Verified" or "NoMatch".</param>
public sealed record GovernmentVerificationDataImportedDomainEvent(Guid GovernmentVerificationDataId, string SubjectType, Guid SubjectId, string Outcome,
    DateTime At) : DomainEvent(At);

/// <param name="Outcome">"Verified" or "Unverified".</param>
public sealed record EducationalCredentialVerificationImportedDomainEvent(Guid EducationalCredentialVerificationId, Guid SubjectId, string Outcome,
    DateTime At) : DomainEvent(At);

/// <param name="Outcome">"Verified" or "Unverified".</param>
public sealed record IdentityVerificationDataImportedDomainEvent(Guid IdentityVerificationDataId, Guid SubjectId, string Outcome, DateTime At)
    : DomainEvent(At);

public sealed record LegacyDataImportedDomainEvent(Guid LegacyDataId, Guid MigrationRunId, string RecordType, string SourceSystem, DateTime At)
    : DomainEvent(At);

public sealed record DataQualityUpdatedDomainEvent(Guid DataQualityId, string FromStatus, string ToStatus, Guid MigrationRunId, int RecordsChecked,
    int RecordsRejected, DateTime At) : DomainEvent(At);
