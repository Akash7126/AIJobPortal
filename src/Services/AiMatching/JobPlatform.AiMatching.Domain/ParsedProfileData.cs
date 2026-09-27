using JobPlatform.SharedKernel.Common.Enums;
using JobPlatform.SharedKernel.Domain;

namespace JobPlatform.AiMatching.Domain;

public sealed class ProfileField : Entity<Guid>
{
    private ProfileField()
    {
    }

    internal ProfileField(ParsedFieldName name, string value, FieldSource source, decimal confidence, bool needsReview)
    {
        Id = Guid.NewGuid();
        Name = name;
        Value = value;
        Source = source;
        Confidence = confidence;
        NeedsReview = needsReview;
    }

    public ParsedFieldName Name { get; private set; }
    public string Value { get; private set; } = string.Empty;
    public FieldSource Source { get; private set; }
    public decimal Confidence { get; private set; }
    public bool NeedsReview { get; private set; }

    internal void SetFromParser(string value, decimal confidence, bool needsReview)
    {
        Value = value;
        Confidence = confidence;
        NeedsReview = needsReview;
    }

    internal void SetFromUser(string value)
    {
        Value = value;
        Source = FieldSource.User;
        Confidence = 100m;
        NeedsReview = false;
    }
}

public sealed record ParsedProfileDataUpdatedDomainEvent(
    DateTime OccurredOnUtc, Guid ParsedProfileDataId, Guid ResumeId, Guid ProfileId, string FromStatus, string ToStatus, Guid ActorId,
    IReadOnlyList<string> ChangedFields, IReadOnlyList<string> Skills) : DomainEvent(OccurredOnUtc);

/// <summary>
/// The job seeker's reviewed/corrected version of what was parsed (AGG-23, handover 3.5). INV-13 only the owner corrects; the corrected value replaces the parsed one;
/// INV-14 a manual correction always wins over a (concurrent) re-parse.
/// </summary>
public sealed class ParsedProfileData : AggregateRoot<Guid>
{
    private readonly List<ProfileField> _fields = new();

    private ParsedProfileData()
    {
    }

    public Guid ResumeId { get; private set; }
    public Guid ProfileId { get; private set; }
    public Guid OwnerAccountId { get; private set; }
    public ReviewStatus Status { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public IReadOnlyList<ProfileField> Fields => _fields;

    public static ParsedProfileData Create(Guid resumeId, Guid profileId, Guid ownerAccountId, IEnumerable<ParsedField> parsed, DateTime nowUtc)
    {
        var data = new ParsedProfileData { Id = Guid.NewGuid(), ResumeId = resumeId, ProfileId = profileId, OwnerAccountId = ownerAccountId, Status = ReviewStatus.Parsed, CreatedAtUtc = nowUtc };
        foreach (var field in parsed)
        {
            data._fields.Add(new ProfileField(field.Name, field.Value, FieldSource.Parser, field.Confidence, field.NeedsReview));
        }

        return data;
    }

    /// <summary>The corrected value replaces the parsed value; the field becomes User-sourced with full confidence and the data is Reviewed.</summary>
    public void Correct(ParsedFieldName name, string value, Actor actor, DateTime nowUtc)
    {
        Guard.Ensure(actor.Type == ActorType.JobSeeker && actor.Id == OwnerAccountId, AiRuleCodes.ParsedNotOwner, "Only the owner may correct the parsed data.",
            AiErrorCodes.Forbidden, BusinessRuleKind.Forbidden);
        Guard.Ensure(!string.IsNullOrWhiteSpace(value), AiRuleCodes.ParsedInvalidField, "A corrected value is required.", AiErrorCodes.InvalidField,
            BusinessRuleKind.InvalidInput);

        var from = Status;
        var field = _fields.FirstOrDefault(f => f.Name == name);
        if (field is null)
        {
            field = new ProfileField(name, value.Trim(), FieldSource.User, 100m, false);
            _fields.Add(field);
        }
        else
        {
            field.SetFromUser(value.Trim());
        }

        Status = ReviewStatus.Reviewed;
        var skills = name == ParsedFieldName.Skills ? TextNormalizer.SplitList(value) : Array.Empty<string>();
        Raise(new ParsedProfileDataUpdatedDomainEvent(nowUtc, Id, ResumeId, ProfileId, from.ToString(), Status.ToString(), actor.Id, new[] { name.ToString() }, skills));
    }

    /// <summary>INV-14: a re-parse replaces parser-sourced fields only; User-sourced fields are kept. Returns the names that were overwritten.</summary>
    public IReadOnlyList<ParsedFieldName> ApplyReparse(Guid newResumeId, IEnumerable<ParsedField> parsed)
    {
        ResumeId = newResumeId;
        var changed = new List<ParsedFieldName>();
        foreach (var incoming in parsed)
        {
            var existing = _fields.FirstOrDefault(f => f.Name == incoming.Name);
            if (existing is null)
            {
                _fields.Add(new ProfileField(incoming.Name, incoming.Value, FieldSource.Parser, incoming.Confidence, incoming.NeedsReview));
                changed.Add(incoming.Name);
            }
            else if (existing.Source == FieldSource.Parser)
            {
                existing.SetFromParser(incoming.Value, incoming.Confidence, incoming.NeedsReview);
                changed.Add(incoming.Name);
            }
        }

        return changed;
    }
}
