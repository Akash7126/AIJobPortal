using JobPlatform.PlatformAdministration.Domain.Common;
using JobPlatform.SharedKernel.Common.ValueObjects;
using JobPlatform.SharedKernel.Domain;

namespace JobPlatform.PlatformAdministration.Domain.Reference;

/// <summary>Static administrator-managed datasets (AGG-14, TERM: Skills, Jobs, Trainings, Datasets).</summary>
public enum ReferenceFileType
{
    Skills,
    Jobs,
    Trainings,
    Datasets
}

public enum ReferenceChangeKind
{
    Add,
    Edit,
    Remove
}

/// <summary>One requested change of a reference file. Edit replaces the name and the active flag of the entry addressed by <see cref="EntryId"/>.</summary>
public sealed record ReferenceChange(ReferenceChangeKind Kind, Guid? EntryId, string? Code, LocalizedText? Name, bool? IsActive);

public sealed class ReferenceEntry : Entity<Guid>
{
    private ReferenceEntry()
    {
        Code = string.Empty;
        Name = new LocalizedText(string.Empty, string.Empty);
    }

    public string Code { get; private set; }

    public LocalizedText Name { get; private set; }

    /// <summary>Removal is soft (data model: soft delete), so existing references keep resolving.</summary>
    public bool IsActive { get; private set; }

    internal static ReferenceEntry Create(string code, LocalizedText name) => new() { Id = Guid.NewGuid(), Code = code, Name = name, IsActive = true };

    internal void Rename(LocalizedText name) => Name = name;

    internal void SetActive(bool active) => IsActive = active;
}

/// <summary>AGG-14 ReferenceFile (story US-3.1.4-07). One file per type; the file version increases by one per saved command.</summary>
public sealed class ReferenceFile : AggregateRoot<Guid>
{
    private readonly List<ReferenceEntry> _entries = new();

    private ReferenceFile()
    {
    }

    public ReferenceFileType Type { get; private set; }

    /// <summary>Incremented once per successful update (a command may carry several entry changes).</summary>
    public int FileVersion { get; private set; }

    public Guid? UpdatedBy { get; private set; }

    public DateTime UpdatedAtUtc { get; private set; }

    public IReadOnlyCollection<ReferenceEntry> Entries => _entries;

    public static ReferenceFile Create(ReferenceFileType type, DateTime nowUtc) =>
        new() { Id = Guid.NewGuid(), Type = type, FileVersion = 1, UpdatedAtUtc = nowUtc };

    /// <summary>Codes of the entries the changes would remove or deactivate: the application asks the owners whether they are still referenced.</summary>
    public IReadOnlyList<string> RemovalCandidateCodes(IEnumerable<ReferenceChange> changes)
    {
        var codes = new List<string>();
        foreach (var change in changes)
        {
            var entry = change.EntryId is { } id ? _entries.FirstOrDefault(e => e.Id == id) : null;
            if (entry is not null && (change.Kind == ReferenceChangeKind.Remove || change.Kind == ReferenceChangeKind.Edit && change.IsActive == false && entry.IsActive))
            {
                codes.Add(entry.Code);
            }
        }

        return codes;
    }

    /// <summary>
    /// Applies the changes as one saved version. INV-04: an entry still referenced (<paramref name="codesInUse"/>) is not removed or deactivated
    /// unless the administrator explicitly confirmed. Concurrent edits: later save wins (the application retries on a lost race).
    /// </summary>
    public void ApplyChanges(IReadOnlyList<ReferenceChange> changes, bool confirmedInUse, IReadOnlyCollection<string> codesInUse, Actor actor, DateTime nowUtc)
    {
        Check(Rules.AdminOnly(actor));
        Rules.EnsureValid(changes.Count == 0, RuleCodes.ReferenceInvalidChange, "At least one change is required.", "changes");

        foreach (var change in changes)
        {
            switch (change.Kind)
            {
                case ReferenceChangeKind.Add:
                    Rules.EnsureValid(string.IsNullOrWhiteSpace(change.Code) || change.Name is null, RuleCodes.ReferenceInvalidChange,
                        "An added entry needs a code and a name.", "changes");
                    Rules.EnsureValid(_entries.Any(e => string.Equals(e.Code, change.Code, StringComparison.OrdinalIgnoreCase)), RuleCodes.ReferenceDuplicateCode,
                        $"The code {change.Code} already exists in this reference file.", "code");
                    _entries.Add(ReferenceEntry.Create(change.Code!.Trim(), change.Name!));
                    break;

                case ReferenceChangeKind.Edit:
                {
                    var entry = Find(change.EntryId);
                    Rules.EnsureValid(change.Name is null && change.IsActive is null, RuleCodes.ReferenceInvalidChange, "An edit needs a name or an active flag.", "changes");
                    if (change.IsActive == false && entry.IsActive)
                    {
                        EnsureNotInUse(entry, confirmedInUse, codesInUse);
                    }

                    if (change.Name is not null)
                    {
                        entry.Rename(change.Name);
                    }

                    if (change.IsActive is { } active)
                    {
                        entry.SetActive(active);
                    }

                    break;
                }

                case ReferenceChangeKind.Remove:
                {
                    var entry = Find(change.EntryId);
                    if (entry.IsActive)
                    {
                        EnsureNotInUse(entry, confirmedInUse, codesInUse);
                    }

                    entry.SetActive(false);
                    break;
                }
            }
        }

        FileVersion++;
        UpdatedBy = actor.Id;
        UpdatedAtUtc = nowUtc;
        Raise(new ReferenceFileUpdatedDomainEvent(Id, Type, FileVersion, actor.Id, nowUtc));
    }

    private ReferenceEntry Find(Guid? entryId)
    {
        var entry = entryId is { } id ? _entries.FirstOrDefault(e => e.Id == id) : null;
        Rules.EnsureValid(entry is null, RuleCodes.ReferenceEntryNotFound, "The entry does not exist in this reference file.", "entryId");
        return entry!;
    }

    private static void EnsureNotInUse(ReferenceEntry entry, bool confirmed, IReadOnlyCollection<string> codesInUse) =>
        Check(new BusinessRule(RuleCodes.ReferenceEntryInUse, $"The entry {entry.Code} is still referenced and needs explicit confirmation to be removed.",
            !confirmed && codesInUse.Contains(entry.Code, StringComparer.OrdinalIgnoreCase), ErrorCodes.EntryInUse, BusinessRuleKind.Conflict));
}
