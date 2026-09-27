using System.Text.RegularExpressions;
using JobPlatform.SharedKernel.Domain;

namespace JobPlatform.AuditLogging.Domain;

/// <summary>Who may see an entry (handover 3.1): the owner type plus, for owned entries, the owner's account id.</summary>
public sealed class OwnerScope : ValueObject
{
    private OwnerScope(OwnerType type, Guid? ownerId)
    {
        Type = type;
        OwnerId = ownerId;
    }

    public OwnerType Type { get; }
    public Guid? OwnerId { get; }

    public static OwnerScope AdminOnly { get; } = new(OwnerType.AdminOnly, null);

    public static OwnerScope Of(OwnerType type, Guid ownerId)
    {
        Guard.Ensure(type != OwnerType.AdminOnly, AuditRuleCodes.InvalidEntry, "An owned scope needs an owner type.");
        Guard.Ensure(ownerId != Guid.Empty, AuditRuleCodes.InvalidEntry, "An owned scope needs an owner id.");
        return new OwnerScope(type, ownerId);
    }

    /// <summary>Parses "AdminOnly" or "&lt;OwnerType&gt;:&lt;guid&gt;" (the form producers put on the audit-record stream). Unparseable input is admin-only (fail closed).</summary>
    public static OwnerScope Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return AdminOnly;
        }

        var parts = text.Split(':', 2);
        return parts.Length == 2 && Enum.TryParse<OwnerType>(parts[0], true, out var type) && type != OwnerType.AdminOnly && Guid.TryParse(parts[1], out var id) && id != Guid.Empty
            ? new OwnerScope(type, id)
            : AdminOnly;
    }

    /// <summary>Rehydration by EF Core.</summary>
    internal static OwnerScope From(OwnerType type, Guid? id) => new(type, id);

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Type;
        yield return OwnerId;
    }
}

/// <summary>INV-02 NO_PII_IN_DETAILS: details carry ids, codes and statuses only - never recipients, bodies, secrets or tokens (THR-038, 3.4.3-04 AC-04).</summary>
public static partial class DetailsPolicy
{
    private static readonly string[] ForbiddenKeyParts =
    {
        "email", "phone", "mobile", "password", "secret", "token", "otp", "body", "message", "name", "address", "nationalid", "iban"
    };

    public static IReadOnlyList<string> Violations(IReadOnlyDictionary<string, string>? details)
    {
        if (details is null)
        {
            return Array.Empty<string>();
        }

        var violations = new List<string>();
        foreach (var (key, value) in details)
        {
            if (ForbiddenKeyParts.Any(part => key.Contains(part, StringComparison.OrdinalIgnoreCase)))
            {
                violations.Add(key);
            }
            else if (EmailPattern().IsMatch(value) || LongDigits().IsMatch(value))
            {
                violations.Add(key);
            }
        }

        return violations;
    }

    /// <summary>Removes every violating entry (for producers on the free-form audit-record stream) and reports how many were dropped.</summary>
    public static Dictionary<string, string> Sanitize(IReadOnlyDictionary<string, string>? details, out int removed)
    {
        var violations = Violations(details).ToHashSet(StringComparer.Ordinal);
        removed = violations.Count;
        return details?.Where(d => !violations.Contains(d.Key)).ToDictionary(d => d.Key, d => d.Value) ?? new Dictionary<string, string>();
    }

    [GeneratedRegex(@"[^@\s]+@[^@\s]+\.[^@\s]+")]
    private static partial Regex EmailPattern();

    // Long digit runs look like phone or national-id numbers; digits inside a GUID or hex id are not.
    [GeneratedRegex(@"(?<![0-9a-fA-F-])\d{9,}(?![0-9a-fA-F-])")]
    private static partial Regex LongDigits();
}

/// <summary>
/// Immutable, append-only record of who did what to what and with which outcome (handover 3.1). Created only through <see cref="Record"/>;
/// there is no update and no delete - the retention policy may only mark it archived (INV-03).
/// </summary>
public sealed class AuditEntry : Entity<Guid>
{
    private AuditEntry()
    {
    }

    public string SourceBc { get; private set; } = string.Empty;

    /// <summary>Message id of the source event; with <see cref="Category"/> it is the redelivery guard (INV-01).</summary>
    public Guid SourceMessageId { get; private set; }

    public AuditCategory Category { get; private set; }
    public DateTime OccurredAtUtc { get; private set; }
    public Guid? ActorId { get; private set; }
    public string? ActorType { get; private set; }
    public string SubjectType { get; private set; } = string.Empty;
    public string SubjectId { get; private set; } = string.Empty;
    public OwnerType OwnerType { get; private set; }
    public Guid? OwnerId { get; private set; }
    public string Action { get; private set; } = string.Empty;
    public AuditOutcome Outcome { get; private set; }
    public string? Code { get; private set; }
    public string DetailsJson { get; private set; } = "{}";
    public DateTime RetainUntilUtc { get; private set; }
    public bool IsArchived { get; private set; }
    public DateTime? ArchivedAtUtc { get; private set; }

    public OwnerScope Scope => OwnerScope.From(OwnerType, OwnerId);

    public IReadOnlyDictionary<string, string> Details =>
        System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(DetailsJson) ?? new Dictionary<string, string>();

    public static AuditEntry Record(string sourceBc, Guid sourceMessageId, AuditCategory category, DateTime occurredAtUtc, Guid? actorId, string? actorType,
        string subjectType, string subjectId, OwnerScope scope, string action, AuditOutcome outcome, string? code,
        IReadOnlyDictionary<string, string>? details, RetentionPolicy retention)
    {
        Guard.Ensure(!string.IsNullOrWhiteSpace(sourceBc) && sourceMessageId != Guid.Empty, AuditRuleCodes.InvalidEntry, "An entry needs its source BC and message id.");
        Guard.Ensure(!string.IsNullOrWhiteSpace(subjectType) && !string.IsNullOrWhiteSpace(subjectId) && !string.IsNullOrWhiteSpace(action),
            AuditRuleCodes.InvalidEntry, "An entry needs a subject and an action.");
        var violations = DetailsPolicy.Violations(details);
        Guard.Ensure(violations.Count == 0, AuditRuleCodes.PiiInDetails, $"Details must not carry personal data or secrets ({string.Join(", ", violations)}).",
            AuditErrorCodes.InvalidField, BusinessRuleKind.InvalidInput);

        return new AuditEntry
        {
            Id = Guid.NewGuid(),
            SourceBc = sourceBc,
            SourceMessageId = sourceMessageId,
            Category = category,
            OccurredAtUtc = occurredAtUtc,
            ActorId = actorId,
            ActorType = actorType,
            SubjectType = subjectType,
            SubjectId = subjectId,
            OwnerType = scope.Type,
            OwnerId = scope.OwnerId,
            Action = action,
            Outcome = outcome,
            Code = code,
            DetailsJson = System.Text.Json.JsonSerializer.Serialize(details ?? new Dictionary<string, string>()),
            RetainUntilUtc = retention.RetainUntil(occurredAtUtc)
        };
    }

    /// <summary>The only mutation an entry ever gets: the retention policy moves it to the archive (kept, not purged).</summary>
    public void Archive(DateTime nowUtc)
    {
        if (IsArchived)
        {
            return;
        }

        IsArchived = true;
        ArchivedAtUtc = nowUtc;
    }
}
