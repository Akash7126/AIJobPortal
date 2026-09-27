using JobPlatform.HelpContent.Domain.Common;
using JobPlatform.SharedKernel.Domain;

namespace JobPlatform.HelpContent.Domain;

public enum HelpKind
{
    Faq,
    Law,
    Regulation,
    ContractTypeRegulation,
    Guide,
    Video,
    InteractiveGuide
}

/// <summary>Q-07 (decided here): laws, regulations and contract-type regulations are HelpContent with a distinguishing Kind rather than a
/// separate legal-library aggregate - the handover offers no story that needs different behaviour for them.</summary>
public enum HelpMediaType
{
    Video,
    InteractiveGuide
}

/// <summary>One immutable version of a help article's text (handover section 3.3: "creates a new version atomically; readers always see one
/// consistent version, never a partial edit").</summary>
public sealed class HelpContentVersion : Entity<int>
{
    private HelpContentVersion()
    {
    }

    internal HelpContentVersion(int versionNo, LocalizedText title, LocalizedText body, Guid editedBy, DateTime editedAtUtc)
    {
        Id = versionNo;
        Title = title;
        Body = body;
        EditedBy = editedBy;
        EditedAtUtc = editedAtUtc;
    }

    public int VersionNo => Id;

    public LocalizedText Title { get; private set; } = default!;

    public LocalizedText Body { get; private set; } = default!;

    public Guid EditedBy { get; private set; }

    public DateTime EditedAtUtc { get; private set; }
}

/// <summary>A video or interactive-guide item on a help article. INV-10: captions or a text alternative are required (THR-067).</summary>
public sealed class HelpMediaItem : Entity<Guid>
{
    private HelpMediaItem()
    {
    }

    internal HelpMediaItem(Guid id, HelpMediaType type, string storageKey, string? captionsRef, string? textAlternative)
    {
        Id = id;
        Type = type;
        StorageKey = storageKey;
        CaptionsRef = captionsRef;
        TextAlternative = textAlternative;
    }

    public HelpMediaType Type { get; private set; }

    public string StorageKey { get; private set; } = string.Empty;

    public string? CaptionsRef { get; private set; }

    public string? TextAlternative { get; private set; }
}

/// <summary>
/// AGG-33: an FAQ/help article, including laws, regulations and contract-type regulations (handover section 3.3). Every edit creates a new
/// version atomically; concurrent edits are "later save wins" (story 3.7.2-05 AC-03) but nothing is lost - every save is retained as a
/// version. Q-02 (decided here): the frozen HelpContentUpdated contract carries FromStatus/ToStatus, but help content has no status of its
/// own (unlike news); both are populated with the constant "Live" and the real signal is FromVersion/ToVersion.
/// </summary>
public sealed class HelpContent : AggregateRoot<Guid>
{
    public const string LiveStatus = "Live";

    private readonly List<HelpContentVersion> _versions = new();
    private readonly List<HelpMediaItem> _media = new();

    private HelpContent()
    {
    }

    public HelpKind Kind { get; private set; }

    public int CurrentVersion { get; private set; }

    public IReadOnlyList<HelpContentVersion> Versions => _versions;

    public IReadOnlyCollection<HelpMediaItem> Media => _media;

    public HelpContentVersion Current => _versions.Single(v => v.VersionNo == CurrentVersion);

    /// <summary>INV-08 title and body required; INV-07 admin-only (E-FAQHC-FORBIDDEN).</summary>
    public static HelpContent Create(Guid id, HelpKind kind, LocalizedText title, LocalizedText body, Actor actor, DateTime nowUtc)
    {
        Check(Rules.AdminOnly(actor, RuleCodes.HelpAdminOnly, ErrorCodes.HelpForbidden));
        Check(RequiredFieldsRule(title, body));

        var content = new HelpContent { Id = id, Kind = kind, CurrentVersion = 1 };
        content._versions.Add(new HelpContentVersion(1, title, body, actor.Id, nowUtc));
        return content;
    }

    /// <summary>Appends a new version atomically and raises HelpContentUpdated (fromVersion/toVersion). Never mutates a prior version.</summary>
    public void Update(LocalizedText title, LocalizedText body, Actor actor, DateTime nowUtc)
    {
        Check(Rules.AdminOnly(actor, RuleCodes.HelpAdminOnly, ErrorCodes.HelpForbidden));
        Check(RequiredFieldsRule(title, body));

        var from = CurrentVersion;
        var to = from + 1;
        _versions.Add(new HelpContentVersion(to, title, body, actor.Id, nowUtc));
        CurrentVersion = to;
        Raise(new HelpContentUpdatedDomainEvent(Id, actor.Id, from, to, nowUtc));
    }

    /// <summary>INV-09 type must be video or interactive guide; INV-10 captions or a text alternative is required.</summary>
    public HelpMediaItem AttachMedia(Guid mediaId, HelpMediaType type, string storageKey, string? captionsRef, string? textAlternative, Actor actor)
    {
        Check(Rules.AdminOnly(actor, RuleCodes.HelpAdminOnly, ErrorCodes.HelpForbidden));
        Check(new BusinessRule(RuleCodes.HelpNoCaptions, "Captions or a text alternative are required for this media.",
            string.IsNullOrWhiteSpace(captionsRef) && string.IsNullOrWhiteSpace(textAlternative), ErrorCodes.HelpUnsupportedFormat));

        var item = new HelpMediaItem(mediaId, type, storageKey, captionsRef, textAlternative);
        _media.Add(item);
        return item;
    }

    private static IBusinessRule RequiredFieldsRule(LocalizedText title, LocalizedText body) =>
        new BusinessRule(RuleCodes.HelpRequiredField, "Title and body are required.", title.IsEmpty || body.IsEmpty, ErrorCodes.HelpRequiredField);
}
