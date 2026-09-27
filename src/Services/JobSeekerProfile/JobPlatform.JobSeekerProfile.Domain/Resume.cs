using JobPlatform.JobSeekerProfile.Domain.Common;
using JobPlatform.SharedKernel.Domain;

namespace JobPlatform.JobSeekerProfile.Domain;

/// <summary>AGG-03 Resume (handover section 3.2): one current resume per profile; a new upload replaces, never duplicates (AC-04).</summary>
public sealed class Resume : AggregateRoot<Guid>
{
    public const long MaxSizeBytes = 10 * 1024 * 1024;
    private static readonly IReadOnlyDictionary<string, ResumeFormat> AllowedContentTypes = new Dictionary<string, ResumeFormat>(StringComparer.OrdinalIgnoreCase)
    {
        ["application/pdf"] = ResumeFormat.Pdf,
        ["application/vnd.openxmlformats-officedocument.wordprocessingml.document"] = ResumeFormat.Docx,
        ["text/plain"] = ResumeFormat.Txt
    };

    private Resume()
    {
    }

    public Guid ProfileId { get; private set; }
    public FileReference File { get; private set; } = null!;
    public ResumeFormat Format { get; private set; }
    public bool IsCurrent { get; private set; }
    public DateTime UploadedAtUtc { get; private set; }

    /// <summary>INV-07 format must be PDF/DOCX/TXT; INV-08 size &lt;= 10 MB. The caller supersedes any previous current resume (INV-09).</summary>
    public static Resume Upload(Guid id, Guid profileId, FileReference file, Guid actorId, DateTime nowUtc)
    {
        Rules.EnsureValid(!AllowedContentTypes.ContainsKey(file.ContentType), RuleCodes.ResumeUnsupportedFormat,
            "The resume must be a PDF, DOCX or TXT file.", ErrorCodes.UnsupportedFormat, "file");
        Rules.EnsureValid(file.SizeBytes > MaxSizeBytes, RuleCodes.ResumeTooLarge, "The resume must be 10 MB or smaller.", ErrorCodes.TooLarge, "file");

        var resume = new Resume
        {
            Id = id,
            ProfileId = profileId,
            File = file,
            Format = AllowedContentTypes[file.ContentType],
            IsCurrent = true,
            UploadedAtUtc = nowUtc
        };
        resume.Raise(new ResumeCreatedDomainEvent(id, profileId, actorId, resume.Format.ToString(), file.SizeBytes, file.Sha256, nowUtc));
        return resume;
    }

    /// <summary>Marks a previously current resume as replaced (called on the old row when a new upload supersedes it).</summary>
    public void MarkSuperseded() => IsCurrent = false;
}
