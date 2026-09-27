namespace JobPlatform.GovernmentIntegration.Domain;

public enum AccessDecisionKind
{
    Allow,
    Deny
}

public sealed record AccessDecision(AccessDecisionKind Kind, string? ErrorCode = null)
{
    public bool IsAllowed => Kind == AccessDecisionKind.Allow;

    public static AccessDecision Allow() => new(AccessDecisionKind.Allow);

    public static AccessDecision Deny(string errorCode) => new(AccessDecisionKind.Deny, errorCode);
}

/// <summary>
/// Domain service realising US-3.4.2-06 (handover section 3.9): stateless, no persisted state of its own. Allow only if the requesting
/// platform component is on the allow-list (INV-17) and the declared purpose is one of the permitted purposes for that component
/// (AC-02 ⇒ E-GDI-FORBIDDEN otherwise). The allow-list below is a proposed decision (the handover does not enumerate it) reflecting the
/// BCs the Event Catalog names as consumers/collaborators: BC-05 (employer verification), BC-04 (credential/identity verification and
/// enrichment), BC-08/administration (migration), and BC-01 itself (background jobs re-authorising on retry).
/// </summary>
public sealed class GovernmentDataAccessPolicy
{
    public const string EmployerOnboardingComponent = "employer-onboarding";
    public const string JobSeekerProfileComponent = "job-seeker-profile";
    public const string PlatformAdministrationComponent = "platform-administration";
    public const string GovernmentIntegrationComponent = "government-integration";

    private static readonly IReadOnlyDictionary<string, IReadOnlySet<AccessPurpose>> AllowList =
        new Dictionary<string, IReadOnlySet<AccessPurpose>>(StringComparer.OrdinalIgnoreCase)
        {
            [EmployerOnboardingComponent] = new HashSet<AccessPurpose> { AccessPurpose.EmployerVerification },
            [JobSeekerProfileComponent] = new HashSet<AccessPurpose>
                { AccessPurpose.CredentialVerification, AccessPurpose.IdentityVerification, AccessPurpose.Enrichment },
            [PlatformAdministrationComponent] = new HashSet<AccessPurpose> { AccessPurpose.Migration },
            [GovernmentIntegrationComponent] = new HashSet<AccessPurpose>
            {
                AccessPurpose.EmployerVerification, AccessPurpose.CredentialVerification, AccessPurpose.IdentityVerification,
                AccessPurpose.Enrichment, AccessPurpose.Migration
            }
        };

    public AccessDecision Authorise(string requestingComponent, AccessPurpose purpose) =>
        AllowList.TryGetValue(requestingComponent, out var purposes) && purposes.Contains(purpose)
            ? AccessDecision.Allow()
            : AccessDecision.Deny(Common.ErrorCodes.AccessForbidden);
}
