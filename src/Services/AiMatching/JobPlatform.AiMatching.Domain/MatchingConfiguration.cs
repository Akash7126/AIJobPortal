using JobPlatform.SharedKernel.Domain;

namespace JobPlatform.AiMatching.Domain;

/// <summary>
/// Weight per criterion, each 0-100, summing to 100 (INV-02; the "normalised weight" reading of the handover, Q-02 flags the interpretation).
/// Immutable: a change is a new value object.
/// </summary>
public sealed class CriterionWeights : ValueObject
{
    /// <summary>Tolerance for the sum (decimal inputs from JSON may carry rounding noise).</summary>
    public const decimal Epsilon = 0.01m;

    private CriterionWeights()
    {
    }

    private CriterionWeights(decimal skill, decimal education, decimal training, decimal location, decimal experience, decimal salary)
    {
        SkillOverlap = skill;
        Education = education;
        Training = training;
        Location = location;
        Experience = experience;
        Salary = salary;
    }

    public decimal SkillOverlap { get; private set; }
    public decimal Education { get; private set; }
    public decimal Training { get; private set; }
    public decimal Location { get; private set; }
    public decimal Experience { get; private set; }
    public decimal Salary { get; private set; }

    public decimal Sum => SkillOverlap + Education + Training + Location + Experience + Salary;

    /// <summary>Platform default: skills matter most (proposed; SRS gives no defaults, A-01).</summary>
    public static CriterionWeights Default { get; } = new(30, 15, 10, 15, 15, 15);

    /// <summary>INV-02: every weight in 0-100 and the sum equal to 100, otherwise E-VBMA-INVALID-FIELD.</summary>
    public static CriterionWeights Create(decimal skill, decimal education, decimal training, decimal location, decimal experience, decimal salary)
    {
        var all = new[] { skill, education, training, location, experience, salary };
        Guard.Ensure(all.All(w => w is >= 0 and <= 100), AiRuleCodes.ConfigOutOfRange, "Every weight must be between 0 and 100.", AiErrorCodes.InvalidField,
            BusinessRuleKind.InvalidInput);
        Guard.Ensure(Math.Abs(all.Sum() - 100m) <= Epsilon, AiRuleCodes.ConfigOutOfRange, "The six weights must sum to 100.", AiErrorCodes.InvalidField,
            BusinessRuleKind.InvalidInput);
        return new CriterionWeights(skill, education, training, location, experience, salary);
    }

    public decimal Of(Criterion criterion) => criterion switch
    {
        Criterion.SkillOverlap => SkillOverlap,
        Criterion.Education => Education,
        Criterion.Training => Training,
        Criterion.Location => Location,
        Criterion.Experience => Experience,
        Criterion.Salary => Salary,
        _ => throw new ArgumentOutOfRangeException(nameof(criterion))
    };

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return SkillOverlap;
        yield return Education;
        yield return Training;
        yield return Location;
        yield return Experience;
        yield return Salary;
    }
}

/// <summary>Immutable copy of the configuration a computation runs with. A run keeps the snapshot it started with, so a mid-run change never affects it (AM.Config.VERSION_AT_START).</summary>
public sealed record MatchingConfigSnapshot(
    int ConfigVersion, decimal MatchThresholdPercent, CriterionWeights Weights, int ShortlistSize, decimal LowConfidenceThresholdPercent)
{
    public static MatchingConfigSnapshot Defaults { get; } = new(1, MatchingConfiguration.DefaultThreshold, CriterionWeights.Default,
        MatchingConfiguration.DefaultShortlistSize, MatchingConfiguration.DefaultLowConfidence);
}

/// <summary>A version of the configuration as it was (history row).</summary>
public sealed class ConfigurationHistoryEntry : Entity<Guid>
{
    private ConfigurationHistoryEntry()
    {
    }

    internal ConfigurationHistoryEntry(MatchingConfigSnapshot snapshot, Guid changedBy, DateTime atUtc)
    {
        Id = Guid.NewGuid();
        ConfigVersion = snapshot.ConfigVersion;
        MatchThresholdPercent = snapshot.MatchThresholdPercent;
        // A distinct instance, never the aggregate's own Weights reference: EF Core's change tracker identifies owned-type instances by
        // CLR reference, and CriterionWeights.Default is a shared singleton reused by every snapshot - aliasing it into two owned slots
        // (MatchingConfiguration.Weights and ConfigurationHistoryEntry.Weights) in the same SaveChanges graph corrupts the change tracker.
        var w = snapshot.Weights;
        Weights = CriterionWeights.Create(w.SkillOverlap, w.Education, w.Training, w.Location, w.Experience, w.Salary);
        ShortlistSize = snapshot.ShortlistSize;
        LowConfidenceThresholdPercent = snapshot.LowConfidenceThresholdPercent;
        ChangedBy = changedBy;
        ChangedAtUtc = atUtc;
    }

    public int ConfigVersion { get; private set; }
    public decimal MatchThresholdPercent { get; private set; }
    public CriterionWeights Weights { get; private set; } = CriterionWeights.Default;
    public int ShortlistSize { get; private set; }
    public decimal LowConfidenceThresholdPercent { get; private set; }
    public Guid ChangedBy { get; private set; }
    public DateTime ChangedAtUtc { get; private set; }
}

public sealed record MatchingConfigurationChangedDomainEvent(DateTime OccurredOnUtc, int ConfigVersion, string Change) : DomainEvent(OccurredOnUtc);

/// <summary>
/// The platform matching configuration: a singleton, versioned aggregate (handover 3.1). Every change bumps <see cref="ConfigVersion"/>.
/// Only administrators may change it (INV-03). No integration event is published (the in-process event evicts the cache).
/// </summary>
public sealed class MatchingConfiguration : AggregateRoot<Guid>
{
    public const decimal DefaultThreshold = 60m;
    public const int DefaultShortlistSize = 100;
    public const decimal DefaultLowConfidence = 70m;

    /// <summary>Well-known id: there is exactly one configuration.</summary>
    public static readonly Guid SingletonId = new("00000000-0000-0000-0000-000000000010");

    private readonly List<ConfigurationHistoryEntry> _history = new();

    private MatchingConfiguration()
    {
    }

    /// <summary>Version of the configuration (not the aggregate's event counter <see cref="AggregateRoot{TId}.Version"/>).</summary>
    public int ConfigVersion { get; private set; }

    public decimal MatchThresholdPercent { get; private set; }
    public CriterionWeights Weights { get; private set; } = CriterionWeights.Default;
    public int ShortlistSize { get; private set; }
    public decimal LowConfidenceThresholdPercent { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }
    public IReadOnlyList<ConfigurationHistoryEntry> History => _history;

    public static MatchingConfiguration CreateDefault(DateTime nowUtc)
    {
        var configuration = new MatchingConfiguration
        {
            Id = SingletonId,
            ConfigVersion = 1,
            MatchThresholdPercent = DefaultThreshold,
            Weights = CriterionWeights.Default,
            ShortlistSize = DefaultShortlistSize,
            LowConfidenceThresholdPercent = DefaultLowConfidence,
            UpdatedAtUtc = nowUtc
        };
        configuration._history.Add(new ConfigurationHistoryEntry(configuration.Snapshot(), Actor.SystemId, nowUtc));
        return configuration;
    }

    public MatchingConfigSnapshot Snapshot() =>
        new(ConfigVersion, MatchThresholdPercent, Weights, ShortlistSize, LowConfidenceThresholdPercent);

    /// <summary>INV-01 0 &lt;= percent &lt;= 100 (E-VBMA-INVALID-FIELD); INV-03 administrators only (E-VBMA-FORBIDDEN).</summary>
    public void ChangeThreshold(decimal percent, Actor actor, DateTime nowUtc)
    {
        RequireAdministrator(actor);
        Guard.Ensure(percent is >= 0 and <= 100, AiRuleCodes.ConfigOutOfRange, "The match threshold must be between 0 and 100.", AiErrorCodes.InvalidField,
            BusinessRuleKind.InvalidInput);
        MatchThresholdPercent = percent;
        Bump(actor, nowUtc, "Threshold");
    }

    /// <summary>INV-02 weights valid and summing to 100 (validated when the value object is created); INV-03 administrators only.</summary>
    public void ChangeWeights(CriterionWeights weights, Actor actor, DateTime nowUtc)
    {
        RequireAdministrator(actor);
        Weights = weights;
        Bump(actor, nowUtc, "Weights");
    }

    private static void RequireAdministrator(Actor actor) =>
        Guard.Ensure(actor.IsAdministrator, AiRuleCodes.ConfigAdminOnly, "Only administrators may change the matching configuration.", AiErrorCodes.Forbidden,
            BusinessRuleKind.Forbidden);

    private void Bump(Actor actor, DateTime nowUtc, string change)
    {
        ConfigVersion++;
        UpdatedAtUtc = nowUtc;
        _history.Add(new ConfigurationHistoryEntry(Snapshot(), actor.Id, nowUtc));
        Raise(new MatchingConfigurationChangedDomainEvent(nowUtc, ConfigVersion, change));
    }
}
