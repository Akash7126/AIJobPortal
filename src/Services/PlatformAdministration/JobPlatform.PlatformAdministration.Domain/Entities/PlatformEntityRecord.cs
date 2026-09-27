using JobPlatform.PlatformAdministration.Domain.Common;
using JobPlatform.SharedKernel.Domain;

namespace JobPlatform.PlatformAdministration.Domain.Entities;

/// <summary>Entity types an administrator may create directly (US-3.1.4-02 AC-02).</summary>
public enum PlatformEntityType
{
    JobSeeker,
    Employer,
    JobOffering
}

public enum PlatformEntityRecordStatus
{
    Created
}

/// <summary>
/// The "core" fields of a record: identical to the required set of the self-service counterpart (INV-01).
/// Names are camelCase; values are trimmed and blank values are dropped.
/// </summary>
public sealed class EntityCore : ValueObject
{
    private readonly SortedDictionary<string, string> _fields;

    private EntityCore(SortedDictionary<string, string> fields) => _fields = fields;

    public IReadOnlyDictionary<string, string> Fields => _fields;

    public string? Get(string name) => _fields.GetValueOrDefault(name);

    public static EntityCore From(IReadOnlyDictionary<string, string>? fields)
    {
        var normalised = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var (name, value) in fields ?? new Dictionary<string, string>())
        {
            if (!string.IsNullOrWhiteSpace(name) && !string.IsNullOrWhiteSpace(value))
            {
                normalised[name.Trim()] = value.Trim();
            }
        }

        return new EntityCore(normalised);
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        foreach (var (name, value) in _fields)
        {
            yield return name;
            yield return value;
        }
    }
}

/// <summary>Required fields and the natural (duplicate-detection) key per entity type.</summary>
public static class EntityCoreSpecification
{
    public const string FullName = "fullName";
    public const string Mobile = "mobile";
    public const string Email = "email";
    public const string CompanyName = "companyName";
    public const string CompanyId = "companyId";
    public const string Title = "title";
    public const string PostingReference = "postingReference";

    /// <summary>Fields that must always be present. A job seeker additionally needs a mobile number or an e-mail (see <see cref="MissingFields"/>).</summary>
    public static IReadOnlyList<string> RequiredFields(PlatformEntityType type) => type switch
    {
        PlatformEntityType.JobSeeker => new[] { FullName },
        PlatformEntityType.Employer => new[] { CompanyName, CompanyId },
        PlatformEntityType.JobOffering => new[] { Title, PostingReference },
        _ => Array.Empty<string>()
    };

    public static IReadOnlyList<string> MissingFields(PlatformEntityType type, EntityCore core)
    {
        var missing = RequiredFields(type).Where(f => core.Get(f) is null).ToList();
        if (type == PlatformEntityType.JobSeeker && core.Get(Mobile) is null && core.Get(Email) is null)
        {
            missing.Add(Mobile);
        }

        return missing;
    }

    /// <summary>Natural key: mobile (else e-mail) of a job seeker, company id of an employer, posting reference of a job offering. Lower-cased.</summary>
    public static string? IdentityKeyOf(PlatformEntityType type, EntityCore core)
    {
        var raw = type switch
        {
            PlatformEntityType.JobSeeker => core.Get(Mobile)?.Replace(" ", string.Empty).Replace("-", string.Empty) ?? core.Get(Email),
            PlatformEntityType.Employer => core.Get(CompanyId),
            PlatformEntityType.JobOffering => core.Get(PostingReference),
            _ => null
        };
        return raw?.Trim().ToLowerInvariant();
    }
}

/// <summary>AGG-11 PlatformEntityRecord: a job seeker, employer or job offering record created directly by an administrator (US-3.1.4-02).</summary>
public sealed class PlatformEntityRecord : AggregateRoot<Guid>
{
    private PlatformEntityRecord()
    {
        Core = EntityCore.From(null);
        IdentityKey = string.Empty;
    }

    public PlatformEntityType EntityType { get; private set; }

    public EntityCore Core { get; private set; }

    /// <summary>Natural key used for duplicate detection (UQ with the entity type).</summary>
    public string IdentityKey { get; private set; }

    public PlatformEntityRecordStatus Status { get; private set; }

    public Guid CreatedBy { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    /// <summary>
    /// Creates the record. Order of checks: INV-03 administrator only (E-AUM-FORBIDDEN), INV-01 required fields (E-AUM-INVALID-FIELD),
    /// INV-02 duplicate (E-AUM-DUPLICATE; <paramref name="keyAlreadyExists"/> is the answer of the repository).
    /// </summary>
    public static PlatformEntityRecord Create(Guid id, PlatformEntityType type, IReadOnlyDictionary<string, string>? core, Actor actor,
        bool keyAlreadyExists, DateTime nowUtc)
    {
        Check(Rules.AdminOnly(actor));
        var entityCore = EntityCore.From(core);
        var missing = EntityCoreSpecification.MissingFields(type, entityCore);
        if (missing.Count > 0)
        {
            throw new BusinessRuleViolationException(RuleCodes.EntityRequiredField, $"Required fields are missing: {string.Join(", ", missing)}.",
                ErrorCodes.InvalidField, BusinessRuleKind.InvalidInput,
                new Dictionary<string, object?> { ["field"] = missing[0], ["violations"] = new[] { RuleCodes.EntityRequiredField } });
        }

        var key = EntityCoreSpecification.IdentityKeyOf(type, entityCore)!;
        Check(new BusinessRule(RuleCodes.EntityDuplicate, "An entity with the same identity already exists.", keyAlreadyExists, ErrorCodes.Duplicate,
            BusinessRuleKind.Conflict));

        var record = new PlatformEntityRecord
        {
            Id = id,
            EntityType = type,
            Core = entityCore,
            IdentityKey = key,
            Status = PlatformEntityRecordStatus.Created,
            CreatedBy = actor.Id,
            CreatedAtUtc = nowUtc
        };
        record.Raise(new PlatformEntityRecordCreatedDomainEvent(id, type, actor.Id, nowUtc));
        return record;
    }
}
