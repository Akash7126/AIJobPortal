using JobPlatform.SharedKernel.Domain;

namespace JobPlatform.JobPosting.Domain;

/// <summary>Externally published error codes of BC-09 (handover section 4; codes added by this implementation are marked).</summary>
public static class ErrorCodes
{
    public const string RequiredField = "E-JCP-REQUIRED-FIELD";
    public const string InvalidField = "E-JCP-INVALID-FIELD";
    public const string UpstreamTimeout = "E-JCP-UPSTREAM-TIMEOUT";
    public const string PostingForbidden = "E-JCP-FORBIDDEN";
    public const string StateActive = "E-JCP-STATE-ACTIVE";
    public const string StatusForbidden = "E-JST-FORBIDDEN";
    public const string StateArchived = "E-JST-STATE-ARCHIVED";

    /// <summary>Added: no external code is given for INV-06 (admin-suspended postings refuse employer activation).</summary>
    public const string AdminSuspended = "E-JST-ADMIN-SUSPENDED";

    /// <summary>Added: a status change outside the transition table (handover section 3.1) that is neither the archived-terminal nor the
    /// renew-only-when-expired case (those keep their own documented codes).</summary>
    public const string InvalidTransition = "E-JST-INVALID-TRANSITION";

    public const string SearchInvalidField = "E-JSF-INVALID-FIELD";
    public const string FavoriteForbidden = "E-JSF-FORBIDDEN";
    public const string InterestedForbidden = "E-JIP-FORBIDDEN";

    /// <summary>Added: 404 of any BC-09 resource.</summary>
    public const string NotFound = "E-JCP-NOT-FOUND";
}

/// <summary>Internal rule codes (foundation section 7 format JP.Aggregate.RULE).</summary>
public static class RuleCodes
{
    public const string PostingRequiredField = "JP.Posting.REQUIRED_FIELD";
    public const string PostingSchemaInvalid = "JP.Posting.SCHEMA_INVALID";
    public const string PostingTaxonomyTimeout = "JP.Posting.TAXONOMY_TIMEOUT";
    public const string PostingNotOwner = "JP.Posting.NOT_OWNER";
    public const string PostingStateActive = "JP.Posting.STATE_ACTIVE";
    public const string PostingStateArchived = "JP.Posting.STATE_ARCHIVED";
    public const string PostingAdminSuspended = "JP.Posting.ADMIN_SUSPENDED";
    public const string PostingDeadlineInPast = "JP.Posting.DEADLINE_IN_PAST";
    public const string PostingInvalidTransition = "JP.Posting.INVALID_TRANSITION";
    public const string SearchInvalidSalaryRange = "JP.Search.INVALID_SALARY_RANGE";
    public const string FavoriteNotOwner = "JP.Favorite.NOT_OWNER";
    public const string SavedSearchNotOwner = "JP.SavedSearch.NOT_OWNER";
    public const string InterestedNotOwner = "JP.Interested.NOT_OWNER";
}

/// <summary>The caller as the domain needs to see it (no framework types).</summary>
public readonly record struct Actor(Guid Id, bool IsAdministrator, bool IsSystem)
{
    public static Actor System(Guid id) => new(id, false, true);
}

internal static class Rules
{
    /// <summary>INV-04 NOT_OWNER: only the employer that owns the posting (or the system, for imports/admin) may change it.</summary>
    public static IBusinessRule PostingOwnerOnly(Actor actor, Guid employerAccountId, string externalCode) =>
        new BusinessRule(RuleCodes.PostingNotOwner, "Only the employer that owns this posting may perform this action.",
            !actor.IsSystem && actor.Id != employerAccountId, externalCode, BusinessRuleKind.Forbidden);

    /// <summary>INV-06 ADMIN_SUSPENDED: an admin-suspended posting cannot be activated/resumed/renewed by the employer.</summary>
    public static IBusinessRule NotAdminSuspended(bool adminSuspended, bool isSystemActor) =>
        new BusinessRule(RuleCodes.PostingAdminSuspended, "An administrator has suspended this posting.", adminSuspended && !isSystemActor,
            ErrorCodes.AdminSuspended, BusinessRuleKind.Conflict);

    /// <summary>A value of the request violates a domain policy: 400 with the field and rule code listed under "errors".</summary>
    public static void EnsureValid(bool broken, string ruleCode, string message, string field, string externalCode = ErrorCodes.InvalidField)
    {
        if (!broken)
        {
            return;
        }

        throw new BusinessRuleViolationException(ruleCode, message, externalCode, BusinessRuleKind.InvalidInput,
            new Dictionary<string, object?> { ["field"] = field, ["violations"] = new[] { ruleCode } });
    }
}
