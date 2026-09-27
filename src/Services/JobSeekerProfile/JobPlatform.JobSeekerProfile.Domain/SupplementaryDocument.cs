using JobPlatform.JobSeekerProfile.Domain.Common;
using JobPlatform.SharedKernel.Domain;

namespace JobPlatform.JobSeekerProfile.Domain;

/// <summary>AGG-05 SupplementaryDocument (handover section 3.4): owner is either a job-seeker profile or (US-3.1.2-08) a company.
/// Allowed formats (Q-04, proposed): PDF, DOCX, JPG, PNG, TXT.</summary>
public sealed class SupplementaryDocument : AggregateRoot<Guid>
{
    public const long MaxSizeBytes = 10 * 1024 * 1024;
    private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "application/pdf", "application/vnd.openxmlformats-officedocument.wordprocessingml.document", "image/jpeg", "image/png", "text/plain"
    };

    private SupplementaryDocument()
    {
    }

    public DocumentOwnerType OwnerType { get; private set; }
    public Guid OwnerId { get; private set; }
    public FileReference File { get; private set; } = null!;
    public string DocumentType { get; private set; } = string.Empty;
    public DateTime UploadedAtUtc { get; private set; }

    /// <summary>INV-12 size/format; the caller supplies whether an identical file (by hash) already exists (INV: duplicate is not created, returned instead).</summary>
    public static SupplementaryDocument Attach(Guid id, DocumentOwnerType ownerType, Guid ownerId, FileReference file, string documentType, DateTime nowUtc)
    {
        var tooLargeCode = ownerType == DocumentOwnerType.Company ? ErrorCodes.CompanyTooLarge : ErrorCodes.TooLarge;
        var unsupportedCode = ownerType == DocumentOwnerType.Company ? ErrorCodes.CompanyUnsupportedFormat : ErrorCodes.UnsupportedFormat;
        Rules.EnsureValid(!AllowedContentTypes.Contains(file.ContentType), RuleCodes.DocumentUnsupportedFormat,
            "Allowed formats: PDF, DOCX, JPG, PNG, TXT.", unsupportedCode, "file");
        Rules.EnsureValid(file.SizeBytes > MaxSizeBytes, RuleCodes.DocumentTooLarge, "The document must be 10 MB or smaller.", tooLargeCode, "file");

        return new SupplementaryDocument
        {
            Id = id,
            OwnerType = ownerType,
            OwnerId = ownerId,
            File = file,
            DocumentType = documentType,
            UploadedAtUtc = nowUtc
        };
    }

    public void EnsureOwnedBy(Actor actor) => Check(Rules.NotOwner(actor, OwnerId, OwnerType));
}
