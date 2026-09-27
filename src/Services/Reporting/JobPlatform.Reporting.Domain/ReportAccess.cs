using JobPlatform.SharedKernel.Domain;

namespace JobPlatform.Reporting.Domain;

/// <summary>
/// Role to report-category permissions (US-3.5.4-08). Roles are identified by an opaque key: an administrator role id from the token's role_id claims
/// (BC-03's role map, resolved per request so a change applies without re-login, AC-04) or the built-in "Administrator" key.
/// </summary>
public sealed class ReportAccessRule : AggregateRoot<Guid>
{
    /// <summary>Key every administrator implicitly carries.</summary>
    public const string AdministratorRole = "Administrator";

    private ReportAccessRule()
    {
    }

    public string Role { get; private set; } = string.Empty;
    public string CategoriesCsv { get; private set; } = string.Empty;
    public DateTime UpdatedAtUtc { get; private set; }

    public IReadOnlyList<ReportCategory> Categories =>
        CategoriesCsv.Length == 0 ? Array.Empty<ReportCategory>() : CategoriesCsv.Split(',').Select(Enum.Parse<ReportCategory>).ToArray();

    public static ReportAccessRule Create(string role, IReadOnlyCollection<ReportCategory> categories, DateTime nowUtc)
    {
        var rule = new ReportAccessRule { Id = Guid.NewGuid() };
        rule.Set(role, categories, nowUtc);
        return rule;
    }

    public void Set(string role, IReadOnlyCollection<ReportCategory> categories, DateTime nowUtc)
    {
        Guard.Ensure(!string.IsNullOrWhiteSpace(role) && role.Length <= 100, ReportingRuleCodes.InvalidDefinition, "A rule needs a role of up to 100 characters.",
            ReportingErrorCodes.CustomInvalidField, BusinessRuleKind.InvalidInput);
        Guard.Ensure(categories.All(Enum.IsDefined), ReportingRuleCodes.InvalidDefinition, "Unknown report category.", ReportingErrorCodes.CustomInvalidField, BusinessRuleKind.InvalidInput);
        Role = role.Trim();
        CategoriesCsv = string.Join(',', categories.Distinct().OrderBy(c => c));
        UpdatedAtUtc = nowUtc;
    }

    public bool IsAllowed(ReportCategory category) => Categories.Contains(category);
}

/// <summary>The decision of the access policy (handover 3.6).</summary>
public static class ReportAccessPolicy
{
    /// <summary>
    /// Denied by default only to non-administrators (the caller must already be an MFA administrator). When none of the caller's roles has a rule the default
    /// applies: administrator-only, i.e. every administrator may (GAP-002). When at least one of the caller's roles has a rule, the caller is allowed exactly the
    /// categories granted by the union of those rules.
    /// </summary>
    public static bool IsAllowed(IReadOnlyCollection<ReportAccessRule> rulesOfCallerRoles, ReportCategory category) =>
        rulesOfCallerRoles.Count == 0 || rulesOfCallerRoles.Any(r => r.IsAllowed(category));
}

public sealed record ReportAccessDecidedDomainEvent(DateTime At, Guid ActorId, ReportCategory Category, bool Allowed, string Request) : DomainEvent(At);

/// <summary>Immutable record of one access decision (AC-03); mapped to the audit-record stream so BC-07 keeps the access log.</summary>
public sealed class ReportAccessDecision : AggregateRoot<Guid>
{
    private ReportAccessDecision()
    {
    }

    public Guid ActorId { get; private set; }
    public ReportCategory Category { get; private set; }
    public bool Allowed { get; private set; }
    public string Request { get; private set; } = string.Empty;
    public DateTime DecidedAtUtc { get; private set; }

    public static ReportAccessDecision Record(Guid actorId, ReportCategory category, bool allowed, string request, DateTime nowUtc)
    {
        var decision = new ReportAccessDecision
        {
            Id = Guid.NewGuid(),
            ActorId = actorId,
            Category = category,
            Allowed = allowed,
            Request = request.Length > 100 ? request[..100] : request,
            DecidedAtUtc = nowUtc
        };
        decision.Raise(new ReportAccessDecidedDomainEvent(nowUtc, actorId, category, allowed, decision.Request));
        return decision;
    }
}
