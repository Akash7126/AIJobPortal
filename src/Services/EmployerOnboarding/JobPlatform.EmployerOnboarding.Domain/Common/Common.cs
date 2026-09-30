using JobPlatform.SharedKernel.Domain;
using JobPlatform.SharedKernel.Domain.Interfaces;

namespace JobPlatform.EmployerOnboarding.Domain.Common;

/// <summary>Externally published error codes of BC-05 (handover section 4).</summary>
public static class ErrorCodes
{
    public const string RegistrationAlreadyApproved = "E-AUM-STATE-APPROVED";
    public const string RegistrationForbidden = "E-AUM-FORBIDDEN";

    /// <summary>Added by this implementation: the handover names the rule but not an external code for it.</summary>
    public const string RegistrationNotPending = "E-AUM-STATE-NOT-PENDING";
    public const string RegistrationProfileNotSubmitted = "E-AUM-PROFILE-NOT-SUBMITTED";

    public const string MediaTooLarge = "E-ERPM-TOO-LARGE";
    public const string MediaUnsupportedFormat = "E-ERPM-UNSUPPORTED-FORMAT";
    public const string MediaForbidden = "E-ERPM-FORBIDDEN";

    /// <summary>Owner-only refusal on an employer self-service resource (registration Level-2, media). Same external code family as MediaForbidden.</summary>
    public const string EmployerForbidden = MediaForbidden;

    /// <summary>Added: 404 of any BC-05 resource.</summary>
    public const string NotFound = "E-EO-NOT-FOUND";

    public const string AccountNotActive = "E-EO-ACCOUNT-NOT-ACTIVE";
}

/// <summary>Internal rule codes (foundation section 7 format EO.Aggregate.RULE).</summary>
public static class RuleCodes
{
    public const string RegistrationNotPending = "EO.Registration.NOT_PENDING";
    public const string RegistrationAlreadyApproved = "EO.Registration.ALREADY_APPROVED";
    public const string RegistrationAdminOnly = "EO.Registration.ADMIN_ONLY";
    public const string RegistrationProfileNotSubmitted = "EO.Registration.PROFILE_NOT_SUBMITTED";
    public const string RegistrationOwnerOnly = "EO.Registration.NOT_OWNER";
    public const string RegistrationAccountNotActive = "EO.Registration.ACCOUNT_NOT_ACTIVE";

    public const string MediaTooLarge = "EO.Media.TOO_LARGE";
    public const string MediaUnsupportedFormat = "EO.Media.UNSUPPORTED_FORMAT";
    public const string MediaNotOwner = "EO.Media.NOT_OWNER";
    public const string MediaAccountNotActive = "EO.Media.ACCOUNT_NOT_ACTIVE";
    public const string MediaNotFound = "EO.Media.NOT_FOUND";
}

/// <summary>The caller as the domain needs to see it (no framework types): who, and whether an administrator.</summary>
public readonly record struct Actor(Guid Id, bool IsAdministrator);

internal static class Rules
{
    /// <summary>INV-03 ADMIN_ONLY - approval is reserved to administrators (403 E-AUM-FORBIDDEN).</summary>
    public static IBusinessRule AdminOnly(Actor actor) =>
        new BusinessRule(RuleCodes.RegistrationAdminOnly, "Only administrators may perform this action.", !actor.IsAdministrator,
            ErrorCodes.RegistrationForbidden, BusinessRuleKind.Forbidden);

    /// <summary>INV-04/INV-08 owner-only manage/remove of an employer's own resources.</summary>
    public static IBusinessRule OwnerOnly(Actor actor, Guid ownerAccountId, string ruleCode, string externalCode) =>
        new BusinessRule(ruleCode, "Only the owning employer may perform this action.", actor.Id != ownerAccountId && !actor.IsAdministrator,
            externalCode, BusinessRuleKind.Forbidden);
}
