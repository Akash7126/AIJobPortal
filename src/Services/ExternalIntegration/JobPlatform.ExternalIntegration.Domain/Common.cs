using JobPlatform.SharedKernel.Domain;
using JobPlatform.SharedKernel.Domain.Interfaces;

namespace JobPlatform.ExternalIntegration.Domain;

/// <summary>Externally published error codes of BC-02 (handover section 4.1). Codes marked "added" fill a gap the handover left without
/// an external code.</summary>
public static class ErrorCodes
{
    // ExternalJobSiteIntegration (EI.Integration.*)
    public const string NotApproved = "E-EJSI-FORBIDDEN";
    public const string AdminOnly = "E-EJSI-FORBIDDEN";

    /// <summary>Added: the handover names EI.Integration.RUN_IN_PROGRESS but gives it no external code.</summary>
    public const string RunInProgress = "E-EJSI-RUN-IN-PROGRESS";

    public const string UpstreamTimeout = "E-EJSI-UPSTREAM-TIMEOUT";

    /// <summary>Added: refusal when an action requires the integration to be Active.</summary>
    public const string IntegrationNotActive = "E-EJSI-NOT-ACTIVE";

    /// <summary>Added: at most one integration per partner account.</summary>
    public const string AlreadyRegistered = "E-EJSI-ALREADY-REGISTERED";

    /// <summary>Owner-only refusal shared by sync-schedule, attribution-visibility and sandbox self-service (3.1.3-05/13).</summary>
    public const string PartnerForbidden = "E-TPJPRI-FORBIDDEN";

    public const string SandboxUpstreamTimeout = "E-TPJPRI-UPSTREAM-TIMEOUT";

    // JobData (AGG-09)
    public const string JobDataInvalidField = "E-TPJPRI-INVALID-FIELD";

    // JobPostAttribution (AGG-10)
    public const string AttributionStateClosed = "E-TPJPRI-STATE-CLOSED";

    // JobDataMapping (AGG-25)
    public const string MappingRequiredFieldUnmapped = "E-TPJPRI-REQUIRED-FIELD";
    public const string MappingNonConformingSource = "E-EJSI-INVALID-FIELD";

    // API framework (ApiVersion)
    public const string ApiUnsupportedFormat = "E-APIF-UNSUPPORTED-FORMAT";
    public const string ApiExpired = "E-APIF-EXPIRED";
    public const string ApiRateLimited = "E-APIF-RATE-LIMITED";

    /// <summary>Added: the handover names EI.Api.ADMIN_ONLY but gives it no external code.</summary>
    public const string ApiAdminOnly = "E-APIF-ADMIN-ONLY";

    /// <summary>Added: retiring before the deprecation window's SunsetAtUtc (3.4.3-05 AC-04 "409 before sunset").</summary>
    public const string ApiRetireBeforeSunset = "E-APIF-RETIRE-BEFORE-SUNSET";

    /// <summary>Added: a state-machine violation of ApiVersion not covered by a more specific code.</summary>
    public const string ApiInvalidTransition = "E-APIF-INVALID-TRANSITION";

    // SoftwareInterfaceConnection (§3.7)
    public const string SoftwareInterfaceUpstreamTimeout = "E-SI-UPSTREAM-TIMEOUT";

    /// <summary>Added: admin/operator-only refusal for the software-interface registry (4.3-01 AC-04).</summary>
    public const string SoftwareInterfaceForbidden = "E-SI-FORBIDDEN";

    /// <summary>Added: 404 of any BC-02 resource (mirrors BC-05's E-EO-NOT-FOUND convention).</summary>
    public const string NotFound = "E-EI-NOT-FOUND";
}

/// <summary>Internal rule codes (foundation section 7 format EI.Aggregate.RULE).</summary>
public static class RuleCodes
{
    public const string IntegrationNotApproved = "EI.Integration.NOT_APPROVED";
    public const string IntegrationAdminOnly = "EI.Integration.ADMIN_ONLY";
    public const string IntegrationRunInProgress = "EI.Integration.RUN_IN_PROGRESS";
    public const string IntegrationOwnerOnly = "EI.Integration.VISIBILITY_OWNER_ONLY";
    public const string IntegrationNotActive = "EI.Integration.NOT_ACTIVE";
    public const string IntegrationAlreadyRegistered = "EI.Integration.ALREADY_REGISTERED";
    public const string IntegrationAtLeastOneModel = "EI.Integration.AT_LEAST_ONE_MODEL";
    public const string IntegrationNoActiveCredential = "EI.Integration.NO_ACTIVE_CREDENTIAL";
    public const string IntegrationNoRunningSync = "EI.Integration.NO_RUNNING_SYNC";

    public const string JobDataRequiredFieldMissing = "EI.JobData.REQUIRED_FIELD_MISSING";
    public const string JobDataSourceIdentityRequired = "EI.JobData.SOURCE_IDENTITY_REQUIRED";

    public const string AttributionStateClosed = "EI.Attribution.STATE_CLOSED";
    public const string AttributionNotActive = "EI.Attribution.NOT_ACTIVE";

    public const string MappingRequiredFieldUnmapped = "EI.Mapping.REQUIRED_FIELD_UNMAPPED";
    public const string MappingNonConformingSource = "EI.Mapping.NON_CONFORMING_SOURCE";

    public const string ApiAdminOnly = "EI.Api.ADMIN_ONLY";
    public const string ApiRetireBeforeSunset = "EI.Api.RETIRE_BEFORE_SUNSET";
    public const string ApiUnsupportedFormat = "EI.Api.UNSUPPORTED_FORMAT";
    public const string ApiNotActive = "EI.Api.NOT_ACTIVE";
    public const string ApiNotDeprecated = "EI.Api.NOT_DEPRECATED";
    public const string ApiDeprecationWindowRequired = "EI.Api.DEPRECATION_WINDOW_REQUIRED";

    public const string SoftwareInterfaceAdminOnly = "EI.SoftwareInterface.ADMIN_ONLY";
}

/// <summary>The caller as the domain needs to see it (no framework types): who, and whether an administrator.</summary>
public readonly record struct Actor(Guid Id, bool IsAdministrator);

internal static class Rules
{
    public static IBusinessRule AdminOnly(Actor actor, string ruleCode = RuleCodes.IntegrationAdminOnly, string externalCode = ErrorCodes.AdminOnly) =>
        new BusinessRule(ruleCode, "Only administrators may perform this action.", !actor.IsAdministrator, externalCode, BusinessRuleKind.Forbidden);

    public static IBusinessRule OwnerOnly(Actor actor, Guid ownerAccountId, string ruleCode = RuleCodes.IntegrationOwnerOnly,
        string externalCode = ErrorCodes.PartnerForbidden) =>
        new BusinessRule(ruleCode, "Only the owning partner may perform this action.", actor.Id != ownerAccountId && !actor.IsAdministrator,
            externalCode, BusinessRuleKind.Forbidden);
}
