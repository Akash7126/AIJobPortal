using JobPlatform.EmployerOnboarding.Domain.Common;
using JobPlatform.SharedKernel.Domain;

namespace JobPlatform.EmployerOnboarding.Domain;

public enum MediaKind
{
    Logo,
    Image,
    RegistrationDocument,
    Other
}

/// <summary>Uploaded file reference. Never the bytes themselves - those live in <see cref="Application.Interfaces.IFileStorage"/>.</summary>
public sealed class FileReference : ValueObject
{
    public FileReference(string storageKey, string fileName, long sizeBytes, string contentType, string sha256)
    {
        StorageKey = storageKey;
        FileName = fileName;
        SizeBytes = sizeBytes;
        ContentType = contentType;
        Sha256 = sha256;
    }

    public string StorageKey { get; }
    public string FileName { get; }
    public long SizeBytes { get; }
    public string ContentType { get; }
    public string Sha256 { get; }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return StorageKey;
        yield return FileName;
        yield return SizeBytes;
        yield return ContentType;
        yield return Sha256;
    }
}

/// <summary>AGG-07: a logo, image or registration document attached to an employer's public presence (handover section 3.2).</summary>
public sealed class CompanyMediaAndDocument : AggregateRoot<Guid>
{
    public const long MaxSizeBytes = 10 * 1024 * 1024;

    private static readonly HashSet<string> ImageContentTypes = new(StringComparer.OrdinalIgnoreCase) { "image/png", "image/jpeg" };
    private static readonly HashSet<string> DocumentContentTypes = new(StringComparer.OrdinalIgnoreCase) { "application/pdf" };

    private CompanyMediaAndDocument()
    {
    }

    public Guid EmployerAccountId { get; private set; }

    public MediaKind Kind { get; private set; }

    public FileReference File { get; private set; } = default!;

    public DateTime UploadedAtUtc { get; private set; }

    public bool IsPrimaryLogo { get; private set; }

    public bool IsRemoved { get; private set; }

    /// <summary>INV-05 the employer account must be active (checked by the caller from the KnownAccounts replica); INV-06 size/format; INV-08 owner-only.
    /// Duplicate detection (INV-07, same SHA-256) is the repository's job (it looks up by hash before calling this factory) - it returns the
    /// existing aggregate instead of creating a new one, so this factory only ever creates a genuinely new upload.</summary>
    public static CompanyMediaAndDocument Attach(Guid id, Guid employerAccountId, MediaKind kind, FileReference file, Actor actor, DateTime nowUtc)
    {
        Check(Rules.OwnerOnly(actor, employerAccountId, RuleCodes.MediaNotOwner, ErrorCodes.MediaForbidden));
        Check(new BusinessRule(RuleCodes.MediaTooLarge, "The file exceeds the 10 MB limit.", file.SizeBytes > MaxSizeBytes,
            ErrorCodes.MediaTooLarge, BusinessRuleKind.BusinessRule));
        var allowed = kind == MediaKind.RegistrationDocument ? DocumentContentTypes : ImageContentTypes.Union(DocumentContentTypes);
        Check(new BusinessRule(RuleCodes.MediaUnsupportedFormat, "The file format is not supported.", !allowed.Contains(file.ContentType),
            ErrorCodes.MediaUnsupportedFormat, BusinessRuleKind.BusinessRule));

        var media = new CompanyMediaAndDocument
        {
            Id = id,
            EmployerAccountId = employerAccountId,
            Kind = kind,
            File = file,
            UploadedAtUtc = nowUtc
        };
        media.Raise(new CompanyMediaAndDocumentCreatedDomainEvent(id, employerAccountId, actor.Id, kind.ToString(), nowUtc));
        media.Raise(new CompanyMediaChangedDomainEvent(employerAccountId, nowUtc));
        return media;
    }

    public void Remove(Actor actor, DateTime nowUtc)
    {
        Check(Rules.OwnerOnly(actor, EmployerAccountId, RuleCodes.MediaNotOwner, ErrorCodes.MediaForbidden));
        IsRemoved = true;
        IsPrimaryLogo = false;
        Raise(new CompanyMediaChangedDomainEvent(EmployerAccountId, nowUtc));
    }

    /// <summary>One primary logo per employer; the previous primary (if any) is unset by the application (it holds the collection, not this aggregate).</summary>
    public void SetAsPrimaryLogo(Actor actor, DateTime nowUtc)
    {
        Check(Rules.OwnerOnly(actor, EmployerAccountId, RuleCodes.MediaNotOwner, ErrorCodes.MediaForbidden));
        Check(new BusinessRule(RuleCodes.MediaUnsupportedFormat, "Only a logo image can be set as the primary logo.", Kind != MediaKind.Logo,
            ErrorCodes.MediaUnsupportedFormat, BusinessRuleKind.BusinessRule));
        IsPrimaryLogo = true;
        Raise(new CompanyMediaChangedDomainEvent(EmployerAccountId, nowUtc));
    }

    public void UnsetPrimaryLogo() => IsPrimaryLogo = false;
}
