using JobPlatform.SharedKernel.Domain;

namespace JobPlatform.GovernmentIntegration.Domain.Common;

/// <summary>Externally published error codes of BC-01 (handover section 4.1/4.2). Where the handover names only an internal rule code
/// (e.g. GI.EmployerVerification.ALREADY_DECIDED) an external "E-GI-*" code is minted here (decision, handover section 12 has no external
/// code catalogue beyond the upstream-timeout/forbidden/invalid-field codes it does list).</summary>
public static class ErrorCodes
{
    public const string Forbidden = "E-GI-FORBIDDEN";
    public const string NotFound = "E-GI-NOT-FOUND";
    public const string SubmissionIncomplete = "E-GI-SUBMISSION-INCOMPLETE";
    public const string AccountNotEmployer = "E-GI-ACCOUNT-NOT-EMPLOYER";
    public const string AlreadyDecided = "E-GI-ALREADY-DECIDED";
    public const string NotInManualReview = "E-GI-NOT-IN-MANUAL-REVIEW";

    /// <summary>US-3.1.2-03 AC-03: automatic employer verification call to MoL/gov DB times out.</summary>
    public const string EmployerVerificationUpstreamTimeout = "E-ERPM-UPSTREAM-TIMEOUT";

    public const string SubjectRequired = "E-GI-SUBJECT-REQUIRED";
    public const string NotRequested = "E-GI-NOT-REQUESTED";

    /// <summary>Government DB / institution / ID system / MoL-PEF integration timeout (US-3.4.2-01/02/03/04).</summary>
    public const string GovDataUpstreamTimeout = "E-GDI-UPSTREAM-TIMEOUT";

    /// <summary>US-3.4.2-06 AC-02: the requesting component/purpose is not on the allow-list.</summary>
    public const string AccessForbidden = "E-GDI-FORBIDDEN";

    /// <summary>US-2.5-01: MoL/PEF reconciliation timeout.</summary>
    public const string ConnectionUpstreamTimeout = "E-CONSTR-UPSTREAM-TIMEOUT";

    public const string MigrationNoActiveRun = "E-GI-NO-ACTIVE-MIGRATION";
    public const string MigrationAlreadyActive = "E-GI-MIGRATION-ALREADY-ACTIVE";
    public const string MigrationPhaseTestFailed = "E-GI-PHASE-TEST-FAILED";
    public const string DuplicateSourceRecord = "E-GI-DUPLICATE-SOURCE-RECORD";
    public const string NotFullyMapped = "E-GI-NOT-FULLY-MAPPED";

    /// <summary>US-6.1-02 AC-03: a legacy record fails validation.</summary>
    public const string LegacyInvalidField = "E-DMIG-INVALID-FIELD";

    public const string DataQualitySnapshotIsolation = "E-GI-SNAPSHOT-ISOLATION";

    /// <summary>US-6.1-01 AC-03: legacy source read timeout.</summary>
    public const string MigrationUpstreamTimeout = "E-DMIG-UPSTREAM-TIMEOUT";
}

/// <summary>Internal rule codes (foundation section 7 format GI.Aggregate.RULE), matching the handover's rule catalogue (section 4.1).</summary>
public static class RuleCodes
{
    public const string EmployerVerificationSubmissionIncomplete = "GI.EmployerVerification.SUBMISSION_INCOMPLETE";
    public const string EmployerVerificationAccountNotEmployer = "GI.EmployerVerification.ACCOUNT_NOT_EMPLOYER";
    public const string EmployerVerificationAlreadyDecided = "GI.EmployerVerification.ALREADY_DECIDED";
    public const string EmployerVerificationNotInManualReview = "GI.EmployerVerification.NOT_IN_MANUAL_REVIEW";
    public const string EmployerVerificationAttemptsExhausted = "GI.EmployerVerification.AUTOMATIC_ATTEMPTS_EXHAUSTED";

    public const string GovDataSubjectRequired = "GI.GovData.SUBJECT_REQUIRED";
    public const string GovDataNotRequested = "GI.GovData.NOT_REQUESTED";
    public const string GovDataAccessNotPermitted = "GI.GovData.ACCESS_NOT_PERMITTED";

    public const string CredentialNoMatchIsNotFailure = "GI.Credential.NO_MATCH_IS_NOT_FAILURE";
    public const string IdentityMustResolveUniquely = "GI.Identity.IDENTITY_MUST_RESOLVE_UNIQUELY";

    public const string LegacyDuplicateSourceRecord = "GI.Legacy.DUPLICATE_SOURCE_RECORD";
    public const string LegacyNotFullyMapped = "GI.Legacy.NOT_FULLY_MAPPED";
    public const string LegacyInvalidField = "GI.Legacy.INVALID_FIELD";

    public const string DataQualitySnapshotIsolation = "GI.DataQuality.SNAPSHOT_ISOLATION";

    public const string MigrationAdminOnly = "GI.Migration.ADMIN_ONLY";
    public const string MigrationPhaseTestFailed = "GI.Migration.PHASE_TEST_FAILED";
    public const string MigrationNoActiveRun = "GI.Migration.NO_ACTIVE_RUN";
    public const string MigrationAlreadyActive = "GI.Migration.ALREADY_ACTIVE";

    public const string ConnectionAdminOnly = "GI.Connection.ADMIN_ONLY";
}

/// <summary>The caller as the domain needs to see it (no framework types): who, and whether an administrator (foundation section 6).</summary>
public readonly record struct Actor(Guid Id, bool IsAdministrator);

internal static class Rules
{
    /// <summary>INV-14/INV-15/INV-16: starting or rolling back a migration, and configuring a source connection, are administrator-only.</summary>
    public static IBusinessRule AdminOnly(Actor actor, string ruleCode) =>
        new BusinessRule(ruleCode, "Only administrators may perform this action.", !actor.IsAdministrator, ErrorCodes.Forbidden, BusinessRuleKind.Forbidden);
}
