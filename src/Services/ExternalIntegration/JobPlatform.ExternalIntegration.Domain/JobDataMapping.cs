using JobPlatform.SharedKernel.Domain;

namespace JobPlatform.ExternalIntegration.Domain;

/// <summary>
/// AGG-25: the field-mapping configuration that standardises a partner's payload to the platform's standard schema. Merges the partner-configured
/// "Data Mapping" (US-3.1.3-04) and the sync-time "standardisation" (US-3.4.1-03) into one aggregate (handover decision D-01).
/// </summary>
public sealed class JobDataMapping : AggregateRoot<Guid>
{
    /// <summary>Standard-schema fields every mapping must cover (INV-09); the same set JobData.Accept requires (INV-07).</summary>
    public static readonly IReadOnlyList<string> RequiredTargetFields = new[] { "title", "summary", "skills" };

    private List<MappingRule> _rules = new();

    private JobDataMapping()
    {
    }

    public Guid IntegrationId { get; private set; }
    public int MappingVersion { get; private set; }
    public string StandardSchemaVersion { get; private set; } = "v1";
    public IReadOnlyList<MappingRule> Rules => _rules;

    public static JobDataMapping Create(Guid id, Guid integrationId, IReadOnlyList<MappingRule> rules, string standardSchemaVersion, Guid actorId,
        DateTime nowUtc)
    {
        ValidateCoverage(rules);
        var mapping = new JobDataMapping
        {
            Id = id,
            IntegrationId = integrationId,
            MappingVersion = 1,
            StandardSchemaVersion = standardSchemaVersion,
            _rules = rules.ToList()
        };
        mapping.Raise(new JobDataMappingUpdatedDomainEvent(id, "None", "Configured", actorId, integrationId, mapping.MappingVersion, nowUtc));
        return mapping;
    }

    /// <summary>A change does not retroactively alter already-imported jobs and never applies to a run already in progress
    /// (enforced by SyncRun.MappingVersion's snapshot, not here) — it only applies to future imports (3.4.1-03 AC-03).</summary>
    public void Configure(IReadOnlyList<MappingRule> rules, Guid actorId, DateTime nowUtc)
    {
        ValidateCoverage(rules);
        _rules = rules.ToList();
        MappingVersion++;
        Raise(new JobDataMappingUpdatedDomainEvent(Id, "Configured", "Configured", actorId, IntegrationId, MappingVersion, nowUtc));
    }

    /// <summary>Applies the configured rules to a partner's raw payload, producing a StandardJob or E-EJSI-INVALID-FIELD when the source
    /// does not conform (3.4.1-03 AC-02).</summary>
    public StandardJob Standardize(IReadOnlyDictionary<string, string> raw)
    {
        var target = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var rule in _rules)
        {
            if (raw.TryGetValue(rule.SourceField, out var value))
            {
                target[rule.TargetField] = Apply(rule.Transform, value);
            }
        }

        foreach (var required in RequiredTargetFields)
        {
            if (!target.ContainsKey(required) || string.IsNullOrWhiteSpace(target[required]))
            {
                throw new BusinessRuleViolationException(RuleCodes.MappingNonConformingSource,
                    $"The source payload does not conform to the mapping: '{required}' could not be produced.",
                    ErrorCodes.MappingNonConformingSource, BusinessRuleKind.InvalidInput);
            }
        }

        var skills = target["skills"].Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        DateTime? deadline = target.TryGetValue("applicationDeadline", out var deadlineText) && DateTime.TryParse(deadlineText, out var parsed)
            ? DateTime.SpecifyKind(parsed, DateTimeKind.Utc)
            : null;

        return new StandardJob(target["title"], target["summary"], skills,
            target.GetValueOrDefault("contractType", "FullTime"), target.GetValueOrDefault("workFormat", "Physical"), deadline,
            target.GetValueOrDefault("location", string.Empty), target.GetValueOrDefault("sourceUrl"));
    }

    /// <summary>INV-09 REQUIRED_FIELD_UNMAPPED: every required standard-schema field must have a source mapping.</summary>
    private static void ValidateCoverage(IReadOnlyList<MappingRule> rules)
    {
        var covered = rules.Select(r => r.TargetField).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var missing = RequiredTargetFields.Where(f => !covered.Contains(f)).ToList();
        if (missing.Count > 0)
        {
            throw new BusinessRuleViolationException(RuleCodes.MappingRequiredFieldUnmapped,
                $"The following required standard-schema fields have no source mapping: {string.Join(", ", missing)}.",
                ErrorCodes.MappingRequiredFieldUnmapped, BusinessRuleKind.InvalidInput);
        }
    }

    private static string Apply(MappingTransform transform, string value) => transform switch
    {
        MappingTransform.Trim => value.Trim(),
        MappingTransform.ToUpper => value.ToUpperInvariant(),
        MappingTransform.ToLower => value.ToLowerInvariant(),
        MappingTransform.SplitComma => value,
        _ => value
    };
}
