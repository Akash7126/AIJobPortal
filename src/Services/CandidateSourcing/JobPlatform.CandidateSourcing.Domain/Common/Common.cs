using JobPlatform.SharedKernel.Domain;

namespace JobPlatform.CandidateSourcing.Domain.Common;

/// <summary>Externally published error codes of BC-11 (handover section 4).</summary>
public static class ErrorCodes
{
    public const string Forbidden = "E-CRFE-FORBIDDEN";
    public const string InvalidField = "E-CRFE-INVALID-FIELD";
    public const string NotFound = "E-CRFE-NOT-FOUND";
}

/// <summary>Internal rule codes (foundation section 7 format CS.Aggregate.RULE).</summary>
public static class RuleCodes
{
    public const string NotOwner = "CS.Recommendation.NOT_OWNER";
    public const string ThresholdOutOfRange = "CS.Threshold.OUT_OF_RANGE";
    public const string SearchInvalidFilters = "CS.Search.INVALID_FILTERS";
    public const string SearchNotVerified = "CS.Search.NOT_VERIFIED";
    public const string TalentPoolNotVisible = "CS.TalentPool.NOT_VISIBLE";
}

/// <summary>The caller as the domain needs to see it: who, and (for the candidate database) whether their employer is verified.</summary>
public readonly record struct Actor(Guid Id);

internal static class Rules
{
    /// <summary>INV-02/INV-05: only the employer that owns the posting/threshold/talent-pool entry may act on it.</summary>
    public static IBusinessRule OwnerOnly(Guid ownerAccountId, Guid callerAccountId) =>
        new BusinessRule(RuleCodes.NotOwner, "Only the owning employer may perform this action.", ownerAccountId != callerAccountId,
            ErrorCodes.Forbidden, BusinessRuleKind.Forbidden);

    public static void EnsureValid(bool broken, string ruleCode, string message, string field)
    {
        if (!broken)
        {
            return;
        }

        throw new BusinessRuleViolationException(ruleCode, message, ErrorCodes.InvalidField, BusinessRuleKind.InvalidInput,
            new Dictionary<string, object?> { ["field"] = field, ["violations"] = new[] { ruleCode } });
    }

    public static void EnsureForbidden(bool broken, string ruleCode, string message) =>
        Check(new BusinessRule(ruleCode, message, broken, ErrorCodes.Forbidden, BusinessRuleKind.Forbidden));

    private static void Check(IBusinessRule rule)
    {
        if (rule.IsBroken())
        {
            throw new BusinessRuleViolationException(rule);
        }
    }
}
