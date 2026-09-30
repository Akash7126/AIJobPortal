using JobPlatform.AiMatching.Domain.Interfaces.Services;

namespace JobPlatform.AiMatching.Domain;

/// <summary>The part of a job seeker's profile scoring needs (ACL view of BC-04; nothing else is stored here).</summary>
public sealed record ProfileMatchView(
    Guid ProfileId, long Version, IReadOnlyList<string> Skills, EducationLevel? Education, IReadOnlyList<string> Training, string? Governorate, string? City,
    IReadOnlyCollection<WorkArrangement> AcceptedArrangements, decimal? YearsOfExperience, decimal? SalaryMin, decimal? SalaryMax);

/// <summary>The part of a posting scoring needs (ACL view of BC-09 enriched with the semantics extracted here).</summary>
public sealed record PostingMatchView(
    Guid PostingId, long Version, bool IsActive, IReadOnlyList<string> RequiredSkills, EducationLevel? RequiredEducation, IReadOnlyList<string> RequiredTraining,
    string? Governorate, string? City, WorkArrangement Arrangement, int? MinYears, int? MaxYears, decimal? SalaryMin, decimal? SalaryMax);

/// <summary>Exact match on normalised text.</summary>
public sealed class ExactSkillSimilarity : ISkillSimilarity
{
    public static readonly ExactSkillSimilarity Instance = new();

    public double Between(string a, string b) => TextNormalizer.Normalize(a) == TextNormalizer.Normalize(b) ? 1d : 0d;
}

/// <summary>Pre-computed pairwise similarities (built by the application layer from embeddings); falls back to exact matching for unknown pairs.</summary>
public sealed class TableSkillSimilarity : ISkillSimilarity
{
    private readonly IReadOnlyDictionary<(string, string), double> _table;

    public TableSkillSimilarity(IReadOnlyDictionary<(string, string), double> table) => _table = table;

    public double Between(string a, string b)
    {
        var (x, y) = (TextNormalizer.Normalize(a), TextNormalizer.Normalize(b));
        if (x == y)
        {
            return 1d;
        }

        return _table.TryGetValue((x, y), out var value) || _table.TryGetValue((y, x), out value) ? value : 0d;
    }
}

/// <param name="Score">0-100.</param>
/// <param name="Weight">The configured weight (0-100).</param>
/// <param name="Included">False when the profile or posting cannot supply the criterion (INV-04: excluded and the remaining weights are re-normalised).</param>
public sealed record CriterionResult(Criterion Criterion, decimal Score, decimal Weight, bool Included);

public sealed record MatchComputation(decimal Score, IReadOnlyList<CriterionResult> Breakdown);

/// <summary>
/// The match scoring algorithm (handover 3.2: the SRS names the criteria, not the formulae, so these are proposed). Pure and deterministic:
/// same inputs, same output. Score = sum(weight_i x criterion_i) / sum(weight_i of included criteria) x 100 (INV-04).
/// </summary>
public sealed class MatchScoringEngine
{
    /// <summary>Overqualification is not penalised; each missing education level costs this fraction.</summary>
    public const double EducationStep = 0.34;

    public const double SameGovernorateScore = 0.7;

    public static MatchScoringEngine Instance { get; } = new();

    public MatchComputation Compute(ProfileMatchView profile, PostingMatchView posting, CriterionWeights weights, ISkillSimilarity similarity)
    {
        var raw = new (Criterion Criterion, double? Value)[]
        {
            (Criterion.SkillOverlap, Overlap(posting.RequiredSkills, profile.Skills, similarity)),
            (Criterion.Education, Education(profile.Education, posting.RequiredEducation)),
            (Criterion.Training, Overlap(posting.RequiredTraining, profile.Training, similarity)),
            (Criterion.Location, Location(profile, posting)),
            (Criterion.Experience, Experience(profile.YearsOfExperience, posting.MinYears, posting.MaxYears)),
            (Criterion.Salary, Salary(profile.SalaryMin, profile.SalaryMax, posting.SalaryMin, posting.SalaryMax))
        };

        var breakdown = new List<CriterionResult>(raw.Length);
        decimal weighted = 0, includedWeight = 0;
        foreach (var (criterion, value) in raw)
        {
            var weight = weights.Of(criterion);
            // A zero-weight criterion contributes nothing either way; a criterion that cannot be scored is excluded (never an error).
            var included = value.HasValue && weight > 0;
            var score = value.HasValue ? Round((decimal)Clamp01(value.Value) * 100m) : 0m;
            breakdown.Add(new CriterionResult(criterion, score, weight, included));
            if (included)
            {
                weighted += weight * (decimal)Clamp01(value!.Value);
                includedWeight += weight;
            }
        }

        var total = includedWeight == 0 ? 0m : Round(weighted / includedWeight * 100m);
        return new MatchComputation(total, breakdown);
    }

    /// <summary>Share of the required terms the candidate covers, each required term counted with its best similarity. Null when either side is empty.</summary>
    internal static double? Overlap(IReadOnlyList<string> required, IReadOnlyList<string> offered, ISkillSimilarity similarity)
    {
        var need = required.Select(TextNormalizer.Normalize).Where(t => t.Length > 0).Distinct().ToArray();
        var have = offered.Select(TextNormalizer.Normalize).Where(t => t.Length > 0).Distinct().ToArray();
        if (need.Length == 0 || have.Length == 0)
        {
            return null;
        }

        return need.Sum(r => have.Max(p => similarity.Between(r, p))) / need.Length;
    }

    internal static double? Education(EducationLevel? profile, EducationLevel? required)
    {
        if (profile is null || required is null)
        {
            return null;
        }

        var gap = (int)required.Value - (int)profile.Value;
        return gap <= 0 ? 1d : Math.Max(0d, 1d - gap * EducationStep);
    }

    internal static double? Location(ProfileMatchView profile, PostingMatchView posting)
    {
        // Remote/hybrid postings are compatible with a candidate who accepts (or has no preference for) that arrangement.
        var accepts = profile.AcceptedArrangements.Count == 0 || profile.AcceptedArrangements.Contains(posting.Arrangement);
        if (posting.Arrangement != WorkArrangement.OnSite && accepts)
        {
            return 1d;
        }

        var profileCity = TextNormalizer.Normalize(profile.City);
        var profileGovernorate = TextNormalizer.Normalize(profile.Governorate);
        var postingCity = TextNormalizer.Normalize(posting.City);
        var postingGovernorate = TextNormalizer.Normalize(posting.Governorate);
        if ((profileCity.Length == 0 && profileGovernorate.Length == 0) || (postingCity.Length == 0 && postingGovernorate.Length == 0))
        {
            return null;
        }

        if (profileCity.Length > 0 && profileCity == postingCity)
        {
            return 1d;
        }

        return profileGovernorate.Length > 0 && profileGovernorate == postingGovernorate ? SameGovernorateScore : 0d;
    }

    internal static double? Experience(decimal? years, int? min, int? max)
    {
        if (years is null || (min is null && max is null))
        {
            return null;
        }

        var y = (double)years.Value;
        var lower = (double)(min ?? 0);
        if (y < lower)
        {
            return Math.Max(0d, 1d - (lower - y) / Math.Max(lower, 1d));
        }

        if (max is { } upper && y > upper)
        {
            return Math.Max(0d, 1d - (y - upper) / Math.Max(upper, 1d) * 0.5);
        }

        return 1d;
    }

    /// <summary>Overlap of the salary expectation range with the offered range. An offer at or above the expectation is a full match.</summary>
    internal static double? Salary(decimal? expectationMin, decimal? expectationMax, decimal? offerMin, decimal? offerMax)
    {
        var lo = expectationMin ?? expectationMax;
        var hi = expectationMax ?? expectationMin;
        if (lo is null || hi is null || (offerMin is null && offerMax is null))
        {
            return null;
        }

        var offeredLow = offerMin ?? 0m;
        var offeredHigh = offerMax ?? decimal.MaxValue;
        if (offeredHigh < lo.Value)
        {
            return Math.Max(0d, 1d - (double)((lo.Value - offeredHigh) / Math.Max(lo.Value, 1m)));
        }

        if (offeredLow >= hi.Value || hi.Value == lo.Value)
        {
            return 1d;
        }

        var overlap = Math.Min(hi.Value, offeredHigh) - Math.Max(lo.Value, offeredLow);
        return (double)(Math.Max(0m, overlap) / (hi.Value - lo.Value));
    }

    private static double Clamp01(double v) => Math.Min(1d, Math.Max(0d, v));

    private static decimal Round(decimal v) => Math.Round(v, 2, MidpointRounding.AwayFromZero);
}
