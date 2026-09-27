using JobPlatform.SharedKernel.Domain;

namespace JobPlatform.PlatformAdministration.Domain.Common;

/// <summary>Externally published error codes of BC-08 (the stories' own codes; the handover marks new ones as added).</summary>
public static class ErrorCodes
{
    public const string Forbidden = "E-AUM-FORBIDDEN";
    public const string Duplicate = "E-AUM-DUPLICATE";
    public const string InvalidField = "E-AUM-INVALID-FIELD";
    public const string StateInactive = "E-AUM-STATE-INACTIVE";

    /// <summary>Added by this implementation: the handover names the rule ENTRY_IN_USE but no external code.</summary>
    public const string EntryInUse = "E-AUM-ENTRY-IN-USE";

    /// <summary>Added: 404 of any BC-08 resource (setting, reference file, taxonomy, offering, entity record).</summary>
    public const string NotFound = "E-AUM-NOT-FOUND";

    /// <summary>Added: BC-03 user directory unreachable (composition query, handover 6.2 "degrade with error notice").</summary>
    public const string UsersUnavailable = "E-AUM-USERS-UNAVAILABLE";
}

/// <summary>Internal rule codes (foundation section 7 format PA.Aggregate.RULE).</summary>
public static class RuleCodes
{
    public const string AdminOnly = "PA.Common.ADMIN_ONLY";
    public const string EntityRequiredField = "PA.Entity.REQUIRED_FIELD";
    public const string EntityDuplicate = "PA.Entity.DUPLICATE";
    public const string SettingInvalidValue = "PA.Setting.INVALID_VALUE";
    public const string SettingOutOfRange = "PA.Setting.OUT_OF_RANGE";
    public const string ReferenceEntryInUse = "PA.Reference.ENTRY_IN_USE";
    public const string ReferenceDuplicateCode = "PA.Reference.DUPLICATE_CODE";
    public const string ReferenceEntryNotFound = "PA.Reference.ENTRY_NOT_FOUND";
    public const string ReferenceInvalidChange = "PA.Reference.INVALID_CHANGE";
    public const string TaxonomyDuplicateCode = "PA.Taxonomy.DUPLICATE_CODE";
    public const string TaxonomyCycle = "PA.Taxonomy.CYCLE";
    public const string TaxonomyParentNotFound = "PA.Taxonomy.PARENT_NOT_FOUND";
    public const string TaxonomyNodeNotFound = "PA.Taxonomy.NODE_NOT_FOUND";
    public const string TaxonomyDepthExceeded = "PA.Taxonomy.DEPTH_EXCEEDED";
    public const string TaxonomyInvalidChange = "PA.Taxonomy.INVALID_CHANGE";
    public const string OfferingAlreadyInactive = "PA.Offering.ALREADY_INACTIVE";
    public const string OfferingReasonRequired = "PA.Offering.REASON_REQUIRED";
}

/// <summary>The caller as the domain needs to see it (no framework types): who, and whether an administrator.</summary>
public readonly record struct Actor(Guid Id, bool IsAdministrator);

internal static class Rules
{
    /// <summary>INV-03 ADMIN_ONLY - every command of this context is reserved to administrators (403 E-AUM-FORBIDDEN).</summary>
    public static IBusinessRule AdminOnly(Actor actor) =>
        new BusinessRule(RuleCodes.AdminOnly, "Only administrators may perform this action.", !actor.IsAdministrator, ErrorCodes.Forbidden,
            BusinessRuleKind.Forbidden);

    /// <summary>A value of the request violates a domain policy: 400 with the field and rule code listed under "errors".</summary>
    public static void EnsureValid(bool broken, string ruleCode, string message, string field)
    {
        if (!broken)
        {
            return;
        }

        throw new BusinessRuleViolationException(ruleCode, message, ErrorCodes.InvalidField, BusinessRuleKind.InvalidInput,
            new Dictionary<string, object?> { ["field"] = field, ["violations"] = new[] { ruleCode } });
    }
}
