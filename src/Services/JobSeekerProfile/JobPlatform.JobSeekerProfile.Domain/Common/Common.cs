using JobPlatform.SharedKernel.Domain;
using JobPlatform.SharedKernel.Domain.Interfaces;

namespace JobPlatform.JobSeekerProfile.Domain.Common;

/// <summary>Externally published error codes of BC-04 (handover section 4, plus additions noted below).</summary>
public static class ErrorCodes
{
    public const string RequiredField = "E-JSRPM-REQUIRED-FIELD";
    public const string Forbidden = "E-JSRPM-FORBIDDEN";
    public const string Conflict = "E-JSRPM-CONFLICT";
    public const string UnsupportedFormat = "E-JSRPM-UNSUPPORTED-FORMAT";
    public const string TooLarge = "E-JSRPM-TOO-LARGE";

    /// <summary>Added by this implementation: 404 of any BC-04 resource.</summary>
    public const string NotFound = "E-JSRPM-NOT-FOUND";

    /// <summary>Added: INV-01 (the handover names the rule but no external code).</summary>
    public const string AccountNotActive = "E-JSRPM-ACCOUNT-NOT-ACTIVE";

    /// <summary>Added: INV-03.</summary>
    public const string Level1Incomplete = "E-JSRPM-LEVEL1-INCOMPLETE";

    /// <summary>Added: INV-05.</summary>
    public const string NoResume = "E-JSRPM-NO-RESUME";

    /// <summary>Added: INV-10.</summary>
    public const string SharingNotActivated = "E-JSRPM-SHARING-NOT-ACTIVATED";

    // Owner type = Company (US-3.1.2-08): the same rules, employer-facing error-code family.
    public const string CompanyForbidden = "E-ERPM-FORBIDDEN";
    public const string CompanyTooLarge = "E-ERPM-TOO-LARGE";
    public const string CompanyUnsupportedFormat = "E-ERPM-UNSUPPORTED-FORMAT";
}

/// <summary>Internal rule codes (foundation section 7 format JP.Aggregate.RULE).</summary>
public static class RuleCodes
{
    public const string AccountNotActive = "JP.Profile.ACCOUNT_NOT_ACTIVE";
    public const string RequiredFieldMissing = "JP.Profile.REQUIRED_FIELD_MISSING";
    public const string Level1Incomplete = "JP.Profile.LEVEL1_INCOMPLETE";
    public const string NotOwner = "JP.Profile.NOT_OWNER";
    public const string NoResume = "JP.Profile.NO_RESUME";
    public const string Conflict = "JP.Profile.CONFLICT";

    public const string ResumeUnsupportedFormat = "JP.Resume.UNSUPPORTED_FORMAT";
    public const string ResumeTooLarge = "JP.Resume.TOO_LARGE";

    public const string ShareLinkNotOwner = "JP.ShareLink.NOT_OWNER";
    public const string SharingNotActivated = "JP.ShareLink.SHARING_NOT_ACTIVATED";

    public const string DocumentTooLarge = "JP.Document.TOO_LARGE";
    public const string DocumentUnsupportedFormat = "JP.Document.UNSUPPORTED_FORMAT";
    public const string DocumentNotOwner = "JP.Document.NOT_OWNER";

    public const string JobPreferenceNotOwner = "JP.JobPreference.NOT_OWNER";

    public const string PrivacyNotOwner = "JP.Privacy.NOT_OWNER";
    public const string DeletionAlreadyPending = "JP.Privacy.DELETION_ALREADY_PENDING";
}

/// <summary>The caller as the domain needs to see it (no framework types).</summary>
public readonly record struct Actor(Guid AccountId);

/// <summary>Where a value/document is attached: the job seeker's own profile, or (US-3.1.2-08) a company.</summary>
public enum DocumentOwnerType
{
    JobSeekerProfile,
    Company
}

internal static class Rules
{
    public static IBusinessRule NotOwner(Actor actor, Guid ownerAccountId) =>
        new BusinessRule(RuleCodes.NotOwner, "Only the owner may perform this action.", ownerAccountId != actor.AccountId,
            ErrorCodes.Forbidden, BusinessRuleKind.Forbidden);

    /// <summary>Same rule, employer-facing error-code family (US-3.1.2-08).</summary>
    public static IBusinessRule NotOwner(Actor actor, Guid ownerAccountId, DocumentOwnerType ownerType) =>
        new BusinessRule(RuleCodes.DocumentNotOwner, "Only the owner may perform this action.", ownerAccountId != actor.AccountId,
            ownerType == DocumentOwnerType.Company ? ErrorCodes.CompanyForbidden : ErrorCodes.Forbidden, BusinessRuleKind.Forbidden);

    /// <summary>File size/format checks are examples of validation errors even though they are enforced defensively in the domain (foundation section 7).</summary>
    public static void EnsureValid(bool broken, string ruleCode, string message, string externalCode, string field)
    {
        if (!broken)
        {
            return;
        }

        throw new BusinessRuleViolationException(ruleCode, message, externalCode, BusinessRuleKind.InvalidInput,
            new Dictionary<string, object?> { ["field"] = field, ["violations"] = new[] { ruleCode } });
    }
}
