using JobPlatform.PlatformAdministration.Domain.Entities;
using JobPlatform.PlatformAdministration.Domain.Offerings;
using JobPlatform.PlatformAdministration.Domain.Reference;
using JobPlatform.SharedKernel.Domain;

namespace JobPlatform.PlatformAdministration.Domain;

public sealed record PlatformEntityRecordCreatedDomainEvent(Guid RecordId, PlatformEntityType EntityType, Guid ActorId, DateTime At) : DomainEvent(At);

public sealed record SystemSettingChangedDomainEvent(Guid SettingId, string Key, int SettingVersion, Guid ActorId, DateTime At) : DomainEvent(At);

public sealed record ReferenceFileUpdatedDomainEvent(Guid FileId, ReferenceFileType Type, int FileVersion, Guid ActorId, DateTime At) : DomainEvent(At);

public sealed record PlatformTaxonomyUpdatedDomainEvent(
    Guid TaxonomyId, string TaxonomyType, int FromVersion, int ToVersion, IReadOnlyList<string> ChangedCodes, Guid ActorId, DateTime At) : DomainEvent(At);

public sealed record JobOfferingSuspendedDomainEvent(Guid JobOfferingId, Guid ActorId, string Reason, ModerationKind Kind, DateTime At) : DomainEvent(At);
