using JobPlatform.SharedKernel.Domain;

namespace JobPlatform.AiMatching.Domain;

public sealed record TaxonomySkill(string Code, string LabelEn, string LabelAr, IReadOnlyList<string> Synonyms);

public sealed record SkillMatch(string Code, decimal Confidence);

/// <summary>An immutable skills taxonomy at one version (BC-08 is the authority). A run captures one and keeps it even when a newer version is published.</summary>
public sealed class SkillTaxonomy
{
    /// <summary>Minimum trigram similarity for a fuzzy (misspelling) match.</summary>
    public const double FuzzyThreshold = 0.8;

    private readonly Dictionary<string, string> _exact = new(StringComparer.Ordinal);
    private readonly List<(string Key, string Code)> _keys = new();

    public SkillTaxonomy(string version, IEnumerable<TaxonomySkill> skills)
    {
        Version = version;
        foreach (var skill in skills)
        {
            foreach (var term in skill.Synonyms.Append(skill.Code).Append(skill.LabelEn).Append(skill.LabelAr))
            {
                var key = TextNormalizer.Normalize(term);
                if (key.Length > 0 && _exact.TryAdd(key, skill.Code))
                {
                    _keys.Add((key, skill.Code));
                }
            }
        }
    }

    public string Version { get; }

    public int Count => _exact.Values.Distinct().Count();

    /// <summary>Every normalised label/synonym with the canonical code it maps to (used to find skill mentions in free text).</summary>
    public IEnumerable<(string Key, string Code)> Terms => _keys;

    /// <summary>Exact (normalised) label/synonym/code match = 100; otherwise the best trigram match at or above the threshold; otherwise null (free text).</summary>
    public SkillMatch? Lookup(string term)
    {
        var key = TextNormalizer.Normalize(term);
        if (key.Length == 0)
        {
            return null;
        }

        if (_exact.TryGetValue(key, out var code))
        {
            return new SkillMatch(code, 100m);
        }

        var best = (Code: (string?)null, Score: 0d);
        foreach (var (candidate, candidateCode) in _keys)
        {
            var similarity = Dice(key, candidate);
            if (similarity > best.Score)
            {
                best = (candidateCode, similarity);
            }
        }

        return best.Score >= FuzzyThreshold && best.Code is not null ? new SkillMatch(best.Code, Math.Round((decimal)best.Score * 100m, 2)) : null;
    }

    private static double Dice(string a, string b)
    {
        if (a.Length < 3 || b.Length < 3)
        {
            return 0;
        }

        var x = Trigrams(a);
        var y = Trigrams(b);
        var common = x.Intersect(y).Count();
        return 2d * common / (x.Count + y.Count);
    }

    private static HashSet<string> Trigrams(string s)
    {
        var set = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i + 3 <= s.Length; i++)
        {
            set.Add(s.Substring(i, 3));
        }

        return set;
    }
}

public sealed class SkillMapping : Entity<Guid>
{
    private SkillMapping()
    {
    }

    internal SkillMapping(string term, SkillMatch? match)
    {
        Id = Guid.NewGuid();
        ExtractedTerm = term;
        CanonicalCode = match?.Code;
        Confidence = match?.Confidence ?? 0m;
        IsFreeText = match is null;
        NeedsReview = match is null;
    }

    public string ExtractedTerm { get; private set; } = string.Empty;
    public string? CanonicalCode { get; private set; }
    public decimal Confidence { get; private set; }
    public bool IsFreeText { get; private set; }
    public bool NeedsReview { get; private set; }
}

public sealed record SkillStandardizationUpdatedDomainEvent(
    DateTime OccurredOnUtc, Guid SkillStandardizationId, Guid ProfileId, string FromStatus, string ToStatus, string TaxonomyVersion) : DomainEvent(OccurredOnUtc);

/// <summary>
/// Maps the terms extracted from a resume to canonical taxonomy skills (AGG-22, handover 3.4). INV-12 a term without a match is kept as free text
/// and flagged for review, never dropped. The taxonomy version in effect when the run started is recorded and used even if a newer one appears mid-run.
/// </summary>
public sealed class SkillStandardization : AggregateRoot<Guid>
{
    private readonly List<SkillMapping> _mappings = new();

    private SkillStandardization()
    {
    }

    public Guid ResumeParsedDataId { get; private set; }
    public Guid ProfileId { get; private set; }
    public string TaxonomyVersion { get; private set; } = string.Empty;
    public StandardizationStatus Status { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }
    public IReadOnlyList<SkillMapping> Mappings => _mappings;

    public static SkillStandardization Standardize(Guid resumeParsedDataId, Guid profileId, IEnumerable<string> terms, SkillTaxonomy taxonomy, DateTime nowUtc)
    {
        var run = new SkillStandardization { Id = Guid.NewGuid(), ResumeParsedDataId = resumeParsedDataId, ProfileId = profileId, Status = StandardizationStatus.None };
        run.Map(terms, taxonomy, nowUtc);
        return run;
    }

    /// <summary>Re-maps the same extracted terms against another taxonomy version (PlatformTaxonomyUpdated). False when it is the version already applied.</summary>
    public bool Restandardize(SkillTaxonomy taxonomy, DateTime nowUtc)
    {
        if (taxonomy.Version == TaxonomyVersion)
        {
            return false;
        }

        var terms = _mappings.Select(m => m.ExtractedTerm).ToArray();
        _mappings.Clear();
        Map(terms, taxonomy, nowUtc);
        return true;
    }

    private void Map(IEnumerable<string> terms, SkillTaxonomy taxonomy, DateTime nowUtc)
    {
        var from = Status;
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var term in terms.Select(t => t.Trim()).Where(t => t.Length > 0))
        {
            if (seen.Add(TextNormalizer.Normalize(term)))
            {
                _mappings.Add(new SkillMapping(term, taxonomy.Lookup(term)));
            }
        }

        TaxonomyVersion = taxonomy.Version;
        UpdatedAtUtc = nowUtc;
        Status = _mappings.Any(m => m.NeedsReview) ? StandardizationStatus.NeedsReview : StandardizationStatus.Standardized;
        Raise(new SkillStandardizationUpdatedDomainEvent(nowUtc, Id, ProfileId, from.ToString(), Status.ToString(), TaxonomyVersion));
    }
}
