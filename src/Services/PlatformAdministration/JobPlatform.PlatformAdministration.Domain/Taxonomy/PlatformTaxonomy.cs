using JobPlatform.PlatformAdministration.Domain.Common;
using JobPlatform.SharedKernel.Common.ValueObjects;
using JobPlatform.SharedKernel.Domain;

namespace JobPlatform.PlatformAdministration.Domain.Taxonomy;

/// <summary>Well-known taxonomy types. The set is open (GAP-003): any identifier matching <see cref="TaxonomyTypes.IsValid"/> is accepted.</summary>
public static class TaxonomyTypes
{
    public const string Skills = "skills";
    public const string Occupations = "occupations";
    public const string TrainingPrograms = "training-programs";
    public const string ReferenceValues = "reference-values";

    public static readonly IReadOnlyList<string> WellKnown = new[] { Skills, Occupations, TrainingPrograms, ReferenceValues };

    public const int MaxLength = 50;

    /// <summary>Lower-case identifier: letters, digits and hyphens, starting with a letter, 2 to 50 characters.</summary>
    public static bool IsValid(string? type) =>
        !string.IsNullOrEmpty(type) && type.Length is >= 2 and <= MaxLength && char.IsAsciiLetterLower(type[0])
        && type.All(c => char.IsAsciiLetterLower(c) || char.IsAsciiDigit(c) || c == '-');

    public static string Normalise(string? type) => (type ?? string.Empty).Trim().ToLowerInvariant();
}

public enum TaxonomyChangeKind
{
    Add,
    Edit,
    Remove
}

/// <summary>
/// One requested change. Add: code, name, optional parent and synonyms. Edit: replaces name, parent, synonyms and active flag of the node
/// (a null parent makes it a root). Remove: soft-remove (IsActive = false) so existing references keep resolving.
/// </summary>
public sealed record TaxonomyChange(TaxonomyChangeKind Kind, string Code, LocalizedText? Name, string? ParentCode, IReadOnlyList<string>? Synonyms, bool? IsActive);

public sealed class TaxonomyNode : Entity<Guid>
{
    private TaxonomyNode()
    {
        Code = string.Empty;
        Name = new LocalizedText(string.Empty, string.Empty);
        Synonyms = Array.Empty<string>();
    }

    public string Code { get; private set; }

    public LocalizedText Name { get; private set; }

    public string? ParentCode { get; private set; }

    public IReadOnlyList<string> Synonyms { get; private set; }

    public bool IsActive { get; private set; }

    internal static TaxonomyNode Create(string code, LocalizedText name, string? parentCode, IReadOnlyList<string>? synonyms) =>
        new() { Id = Guid.NewGuid(), Code = code, Name = name, ParentCode = parentCode, Synonyms = synonyms ?? Array.Empty<string>(), IsActive = true };

    internal void Update(LocalizedText name, string? parentCode, IReadOnlyList<string> synonyms, bool active)
    {
        Name = name;
        ParentCode = parentCode;
        Synonyms = synonyms;
        IsActive = active;
    }
}

/// <summary>The nodes of one saved version, kept so a consumer can validate against "the version in effect at submission" (immutable per version).</summary>
public sealed class TaxonomySnapshot : Entity<Guid>
{
    private TaxonomySnapshot()
    {
        Type = string.Empty;
        Nodes = Array.Empty<TaxonomyNodeData>();
    }

    public Guid TaxonomyId { get; private set; }

    public string Type { get; private set; }

    public int TaxonomyVersion { get; private set; }

    public IReadOnlyList<TaxonomyNodeData> Nodes { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    internal static TaxonomySnapshot Of(PlatformTaxonomy taxonomy, DateTime nowUtc) => new()
    {
        Id = Guid.NewGuid(),
        TaxonomyId = taxonomy.Id,
        Type = taxonomy.Type,
        TaxonomyVersion = taxonomy.TaxonomyVersion,
        Nodes = taxonomy.Nodes.OrderBy(n => n.Code, StringComparer.Ordinal)
            .Select(n => new TaxonomyNodeData(n.Code, n.Name.Ar, n.Name.En, n.ParentCode, n.Synonyms, n.IsActive)).ToArray(),
        CreatedAtUtc = nowUtc
    };
}

/// <summary>Plain data of a node inside a snapshot (serialised as JSON by the infrastructure).</summary>
public sealed record TaxonomyNodeData(string Code, string NameAr, string NameEn, string? ParentCode, IReadOnlyList<string> Synonyms, bool IsActive);

/// <summary>AGG-15 PlatformTaxonomy (story US-3.1.4-08): skills, occupations, training programs, ... with a version bumped on every saved change.</summary>
public sealed class PlatformTaxonomy : AggregateRoot<Guid>
{
    public const int MaxDepth = 6;

    private readonly List<TaxonomyNode> _nodes = new();

    private PlatformTaxonomy()
    {
        Type = string.Empty;
    }

    public string Type { get; private set; }

    /// <summary>Starts at 1 (empty); +1 per saved change.</summary>
    public int TaxonomyVersion { get; private set; }

    public Guid? UpdatedBy { get; private set; }

    public DateTime UpdatedAtUtc { get; private set; }

    public IReadOnlyCollection<TaxonomyNode> Nodes => _nodes;

    public static PlatformTaxonomy Create(string type, DateTime nowUtc)
    {
        var normalised = TaxonomyTypes.Normalise(type);
        Rules.EnsureValid(!TaxonomyTypes.IsValid(normalised), RuleCodes.TaxonomyInvalidChange, "The taxonomy type is not a valid identifier.", "type");
        return new PlatformTaxonomy { Id = Guid.NewGuid(), Type = normalised, TaxonomyVersion = 1, UpdatedAtUtc = nowUtc };
    }

    /// <summary>The nodes of the current version as an immutable snapshot (the application stores one per version).</summary>
    public TaxonomySnapshot CreateSnapshot(DateTime nowUtc) => TaxonomySnapshot.Of(this, nowUtc);

    /// <summary>
    /// Applies the changes as one saved version. INV-05: codes are unique within the taxonomy, parents exist, no cycles, depth at most six.
    /// INV-06: later save wins (the application retries a lost race) and the change is logged by the event and the version bump.
    /// </summary>
    public void ApplyChanges(IReadOnlyList<TaxonomyChange> changes, Actor actor, DateTime nowUtc)
    {
        Check(Rules.AdminOnly(actor));
        Rules.EnsureValid(changes.Count == 0, RuleCodes.TaxonomyInvalidChange, "At least one change is required.", "changes");

        // Work on a copy of the structure, validate the result as a whole, and only then touch the entities.
        var working = _nodes.ToDictionary(n => n.Code, n => new Working(n.Code, n.Name, n.ParentCode, n.Synonyms, n.IsActive), StringComparer.OrdinalIgnoreCase);
        var changed = new List<string>();
        foreach (var change in changes)
        {
            var code = change.Code.Trim();
            switch (change.Kind)
            {
                case TaxonomyChangeKind.Add:
                    Rules.EnsureValid(change.Name is null, RuleCodes.TaxonomyInvalidChange, "An added node needs a name.", "name");
                    Rules.EnsureValid(working.ContainsKey(code), RuleCodes.TaxonomyDuplicateCode, $"The code {code} already exists in this taxonomy.", "code");
                    working[code] = new Working(code, change.Name!, Blank(change.ParentCode), change.Synonyms ?? Array.Empty<string>(), true) { IsNew = true };
                    break;

                case TaxonomyChangeKind.Edit:
                {
                    var node = Find(working, code);
                    working[node.Code] = node with
                    {
                        Name = change.Name ?? node.Name,
                        ParentCode = Blank(change.ParentCode),
                        Synonyms = change.Synonyms ?? node.Synonyms,
                        IsActive = change.IsActive ?? node.IsActive,
                        Touched = true
                    };
                    break;
                }

                case TaxonomyChangeKind.Remove:
                {
                    var node = Find(working, code);
                    working[node.Code] = node with { IsActive = false, Touched = true };
                    break;
                }
            }

            changed.Add(code);
        }

        ValidateStructure(working);

        foreach (var node in working.Values)
        {
            if (node.IsNew)
            {
                _nodes.Add(TaxonomyNode.Create(node.Code, node.Name, node.ParentCode, node.Synonyms));
            }
            else if (node.Touched)
            {
                _nodes.First(n => string.Equals(n.Code, node.Code, StringComparison.OrdinalIgnoreCase)).Update(node.Name, node.ParentCode, node.Synonyms, node.IsActive);
            }
        }

        var from = TaxonomyVersion;
        TaxonomyVersion++;
        UpdatedBy = actor.Id;
        UpdatedAtUtc = nowUtc;
        Raise(new PlatformTaxonomyUpdatedDomainEvent(Id, Type, from, TaxonomyVersion, changed.Distinct(StringComparer.OrdinalIgnoreCase).ToArray(), actor.Id, nowUtc));
    }

    private static string? Blank(string? code) => string.IsNullOrWhiteSpace(code) ? null : code.Trim();

    private static Working Find(Dictionary<string, Working> working, string code)
    {
        Rules.EnsureValid(!working.TryGetValue(code, out _), RuleCodes.TaxonomyNodeNotFound, $"The code {code} does not exist in this taxonomy.", "code");
        return working[code];
    }

    private static void ValidateStructure(Dictionary<string, Working> working)
    {
        foreach (var node in working.Values)
        {
            Rules.EnsureValid(node.ParentCode is not null && !working.ContainsKey(node.ParentCode), RuleCodes.TaxonomyParentNotFound,
                $"The parent {node.ParentCode} of {node.Code} does not exist.", "parentCode");
        }

        foreach (var node in working.Values)
        {
            var depth = 1;
            var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { node.Code };
            for (var parent = node.ParentCode; parent is not null; parent = working[parent].ParentCode)
            {
                Rules.EnsureValid(!visited.Add(parent), RuleCodes.TaxonomyCycle, $"{node.Code} would become its own ancestor.", "parentCode");
                depth++;
            }

            Rules.EnsureValid(depth > MaxDepth, RuleCodes.TaxonomyDepthExceeded, $"The hierarchy of {node.Code} is deeper than {MaxDepth} levels.", "parentCode");
        }
    }

    private sealed record Working(string Code, LocalizedText Name, string? ParentCode, IReadOnlyList<string> Synonyms, bool IsActive)
    {
        public bool IsNew { get; init; }

        public bool Touched { get; init; }
    }
}
