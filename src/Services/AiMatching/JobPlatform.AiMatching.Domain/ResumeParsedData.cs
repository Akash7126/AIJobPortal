using JobPlatform.SharedKernel.Domain;

namespace JobPlatform.AiMatching.Domain;

/// <summary>One field the parser extracted, before the aggregate applies the confidence rules.</summary>
public sealed record ParsedFieldInput(ParsedFieldName Name, string Value, decimal Confidence);

/// <summary>Output of the resume parser port: fields with per-field confidence plus the non-PII facts published to BC-04.</summary>
public sealed record ParserResult(
    TextLanguage Language, IReadOnlyList<ParsedFieldInput> Fields, IReadOnlyList<string> Skills, IReadOnlyList<string> JobTitles, int? YearsOfExperience,
    string ModelVersion);

public sealed class ParsedField : Entity<Guid>
{
    private ParsedField()
    {
    }

    internal ParsedField(ParsedFieldName name, string value, decimal confidence, bool needsReview)
    {
        Id = Guid.NewGuid();
        Name = name;
        Value = value;
        Confidence = confidence;
        NeedsReview = needsReview;
    }

    public ParsedFieldName Name { get; private set; }
    public string Value { get; private set; } = string.Empty;
    public decimal Confidence { get; private set; }
    public bool NeedsReview { get; private set; }
}

public sealed record ResumeParsedDataComputedDomainEvent(
    DateTime OccurredOnUtc, Guid ResumeParsedDataId, Guid ResumeId, Guid ProfileId, TextLanguage Language, ParseStatus Status,
    IReadOnlyList<string> Skills, IReadOnlyList<string> JobTitles, int? YearsOfExperience) : DomainEvent(OccurredOnUtc);

/// <summary>
/// The structured content extracted from one resume (handover 3.3). INV-08 an unreadable file is stored as Failed (E-VBMA-UNSUPPORTED-FORMAT);
/// INV-09 an unsupported language is parsed best effort with reduced, flagged confidence; INV-10 a field needs review below the low-confidence
/// threshold (default 70 %: 69 flagged, 70 not); INV-11 parse plus score within 5 s is a runtime budget (checked by the handler).
/// </summary>
public sealed class ResumeParsedData : AggregateRoot<Guid>
{
    /// <summary>Confidence multiplier applied when the language is not supported.</summary>
    public const decimal UnsupportedLanguageFactor = 0.75m;

    private readonly List<ParsedField> _fields = new();

    private ResumeParsedData()
    {
    }

    public Guid ResumeId { get; private set; }
    public Guid ProfileId { get; private set; }
    public string Sha256 { get; private set; } = string.Empty;
    public TextLanguage Language { get; private set; }
    public ParseStatus Status { get; private set; }
    public string? FailureCode { get; private set; }
    public bool LanguageFlagged { get; private set; }
    public string ModelVersion { get; private set; } = string.Empty;
    public Guid? SupersededBy { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public IReadOnlyList<ParsedField> Fields => _fields;
    public IReadOnlyList<string> Skills { get; private set; } = Array.Empty<string>();

    public static ResumeParsedData FromParserResult(Guid resumeId, Guid profileId, string sha256, ParserResult result, decimal lowConfidenceThresholdPercent,
        DateTime nowUtc)
    {
        var flagged = result.Language == TextLanguage.Other;
        var data = new ResumeParsedData
        {
            Id = Guid.NewGuid(), ResumeId = resumeId, ProfileId = profileId, Sha256 = sha256, Language = result.Language, Status = ParseStatus.Parsed,
            LanguageFlagged = flagged, ModelVersion = result.ModelVersion, CreatedAtUtc = nowUtc, Skills = result.Skills.ToArray()
        };
        foreach (var field in result.Fields)
        {
            var confidence = Math.Round(Math.Min(100m, Math.Max(0m, field.Confidence)) * (flagged ? UnsupportedLanguageFactor : 1m), 2);
            // Best effort in an unsupported language is always flagged for the user to review (US-3.3.1-09 AC-03).
            data._fields.Add(new ParsedField(field.Name, field.Value, confidence, flagged || confidence < lowConfidenceThresholdPercent));
        }

        data.Raise(new ResumeParsedDataComputedDomainEvent(nowUtc, data.Id, resumeId, profileId, data.Language, data.Status, data.Skills, result.JobTitles,
            result.YearsOfExperience));
        return data;
    }

    /// <summary>INV-08: the file could not be read at all. Kept as a Failed record so the outcome is published and the parse is not retried forever.</summary>
    public static ResumeParsedData Unreadable(Guid resumeId, Guid profileId, string sha256, string modelVersion, DateTime nowUtc)
    {
        var data = new ResumeParsedData
        {
            Id = Guid.NewGuid(), ResumeId = resumeId, ProfileId = profileId, Sha256 = sha256, Language = TextLanguage.Other, Status = ParseStatus.Failed,
            FailureCode = AiErrorCodes.UnsupportedFormat, ModelVersion = modelVersion, CreatedAtUtc = nowUtc
        };
        data.Raise(new ResumeParsedDataComputedDomainEvent(nowUtc, data.Id, resumeId, profileId, data.Language, data.Status, Array.Empty<string>(),
            Array.Empty<string>(), null));
        return data;
    }

    /// <summary>The same resume was replaced: this record is no longer the current parse.</summary>
    public void Supersede(Guid newId)
    {
        Guard.Ensure(newId != Id, AiRuleCodes.InvalidInput, "A parse cannot supersede itself.");
        SupersededBy = newId;
    }

    public IReadOnlyList<ParsedField> FieldsNeedingReview => _fields.Where(f => f.NeedsReview).ToArray();
}
