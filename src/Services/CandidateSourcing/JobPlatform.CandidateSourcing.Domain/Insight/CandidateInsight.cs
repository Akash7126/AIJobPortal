using JobPlatform.SharedKernel.Domain;

namespace JobPlatform.CandidateSourcing.Domain.Insight;

public sealed record CandidateInsightComputedDomainEvent(
    Guid CandidateInsightId, Guid JobPostingId, Guid EmployerAccountId, Guid CandidateProfileId, Guid ActorId,
    string? Availability, decimal? ExpectedSalary, decimal? FitScore, IReadOnlyList<string> WithheldFields, DateTime OccurredOnUtc)
    : DomainEvent(OccurredOnUtc);

/// <summary>One breakdown criterion of the match score, restated in this BC's language (Q-06: strength/gap thresholds).</summary>
public sealed record FitCriterion(string Criterion, decimal Score, bool IsStrength, bool IsGap);

/// <summary>
/// The overall fit derived from the match-score breakdown. Q-06 (proposed, configurable): a criterion scoring &gt;= <see cref="StrengthThreshold"/>
/// is a strength; one scoring &lt; <see cref="GapThreshold"/> is a gap. Criteria the candidate withheld are never scored - see <see cref="CandidateInsight.Compute"/>.
/// </summary>
public sealed class Fit
{
    public const decimal StrengthThreshold = 75m;
    public const decimal GapThreshold = 40m;

    private Fit(decimal overallScore, IReadOnlyList<FitCriterion> criteria)
    {
        OverallScore = overallScore;
        Criteria = criteria;
    }

    public decimal OverallScore { get; }

    public IReadOnlyList<FitCriterion> Criteria { get; }

    public IEnumerable<string> Strengths => Criteria.Where(c => c.IsStrength).Select(c => c.Criterion);

    public IEnumerable<string> Gaps => Criteria.Where(c => c.IsGap).Select(c => c.Criterion);

    public static Fit From(decimal overallScore, IReadOnlyList<(string Criterion, decimal Score)> included) =>
        new(overallScore, included.Select(c => new FitCriterion(c.Criterion, c.Score, c.Score >= StrengthThreshold, c.Score < GapThreshold)).ToArray());

    /// <summary>Reconstructs an already-computed Fit (e.g. from persistence) without recomputing strengths/gaps.</summary>
    public static Fit Restore(decimal overallScore, IReadOnlyList<FitCriterion> criteria) => new(overallScore, criteria);
}

/// <summary>
/// Proposed aggregate (US-3.3.3-06, implemented in BC-11 per the handover's Q-01 recommendation): availability, salary expectation and fit computed
/// for one (employer, posting, candidate) triple. INV-07/08: a field the candidate restricted is withheld and reported as "unavailable", never a default.
/// </summary>
public sealed class CandidateInsight : AggregateRoot<Guid>
{
    private readonly List<string> _withheldFields = new();

    private CandidateInsight()
    {
    }

    public Guid EmployerAccountId { get; private set; }

    public Guid JobPostingId { get; private set; }

    public Guid CandidateProfileId { get; private set; }

    /// <summary>Null means "unavailable" (INV-08) - the candidate never disclosed it, not a zero/default value.</summary>
    public string? Availability { get; private set; }

    public decimal? ExpectedSalaryMin { get; private set; }

    public decimal? ExpectedSalaryMax { get; private set; }

    public Fit Fit { get; private set; } = null!;

    public IReadOnlyList<string> WithheldFields => _withheldFields;

    public DateTime ComputedAtUtc { get; private set; }

    /// <summary>
    /// disclosedFields names the fields the candidate's privacy settings allow this employer to see (from BC-04's candidate view);
    /// anything not listed there is withheld regardless of what was supplied (INV-07).
    /// </summary>
    public static CandidateInsight Compute(
        Guid employerAccountId, Guid jobPostingId, Guid candidateProfileId, Guid actorId, IReadOnlyCollection<string> disclosedFields,
        string? availability, decimal? expectedSalaryMin, decimal? expectedSalaryMax, decimal overallScore,
        IReadOnlyList<(string Criterion, decimal Score, bool Included)> breakdown, DateTime nowUtc)
    {
        var insight = new CandidateInsight
        {
            Id = Guid.NewGuid(),
            EmployerAccountId = employerAccountId,
            JobPostingId = jobPostingId,
            CandidateProfileId = candidateProfileId,
            ComputedAtUtc = nowUtc
        };

        var disclosesAvailability = disclosedFields.Contains("availability", StringComparer.OrdinalIgnoreCase);
        insight.Availability = disclosesAvailability ? availability : null;
        if (!disclosesAvailability)
        {
            insight._withheldFields.Add("availability");
        }

        var disclosesSalary = disclosedFields.Contains("salary", StringComparer.OrdinalIgnoreCase);
        insight.ExpectedSalaryMin = disclosesSalary ? expectedSalaryMin : null;
        insight.ExpectedSalaryMax = disclosesSalary ? expectedSalaryMax : null;
        if (!disclosesSalary)
        {
            insight._withheldFields.Add("salary");
        }

        var included = breakdown.Where(b => b.Included).Select(b => (b.Criterion, b.Score)).ToArray();
        insight.Fit = Fit.From(overallScore, included);

        insight.Raise(new CandidateInsightComputedDomainEvent(insight.Id, jobPostingId, employerAccountId, candidateProfileId, actorId,
            insight.Availability, insight.ExpectedSalaryMax, overallScore, insight._withheldFields.ToArray(), nowUtc));
        return insight;
    }
}
