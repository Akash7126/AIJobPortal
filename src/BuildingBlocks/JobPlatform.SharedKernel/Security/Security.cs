namespace JobPlatform.SharedKernel.Security;

public static class Roles
{
    public const string JobSeeker = "JobSeeker";
    public const string Employer = "Employer";
    public const string Administrator = "Administrator";
    public const string ExternalJobSite = "ExternalJobSite";
    public const string Guest = "Guest";
}

/// <summary>Permission strings evaluated by the RBAC engine (THR-031). Role membership is resolved per request.</summary>
public static class Permissions
{
    public const string AccountsRead = "accounts.read";
    public const string AccountsApprove = "accounts.approve";
    public const string AccountsBan = "accounts.ban";
    public const string AccountsDeactivate = "accounts.deactivate";
    public const string AccountsResetCredentials = "accounts.reset-credentials";
    public const string AccountsApprovePartner = "accounts.approve-partner";
    public const string AccountsAssignRoles = "accounts.assign-roles";
    public const string RolesRead = "roles.read";
    public const string RolesManage = "roles.manage";
    public const string PasswordPolicyManage = "password-policy.manage";
    public const string SessionTimeoutManage = "session-timeout.manage";
    public const string AccessLogRead = "access-log.read";
    public const string ApiCredentialsManage = "api-credentials.manage";
    public const string ProfileManage = "profile.manage";
    public const string EmployerManage = "employer.manage";
    public const string JobsBrowse = "jobs.browse";
    public const string JobsImport = "jobs.import";

    public static readonly IReadOnlyCollection<string> All = new[]
    {
        AccountsRead, AccountsApprove, AccountsBan, AccountsDeactivate, AccountsResetCredentials, AccountsApprovePartner,
        AccountsAssignRoles, RolesRead, RolesManage, PasswordPolicyManage, SessionTimeoutManage, AccessLogRead,
        ApiCredentialsManage, ProfileManage, EmployerManage, JobsBrowse, JobsImport
    };
}

/// <summary>Claim names used in access tokens (shared so every BC validating tokens reads the same names).</summary>
public static class AppClaimTypes
{
    public const string ActorType = "actor_type";
    public const string RoleId = "role_id";
    public const string SessionId = "sid";
    public const string Amr = "amr";
    public const string ClientId = "client_id";
    public const string Scope = "scope";
    public const string MustChangePassword = "pwd_change";
    public const string MfaValue = "mfa";
}

public static class Scopes
{
    /// <summary>Service-to-service scope required by /internal/v1/* endpoints.</summary>
    public const string Internal = "identity.internal";
    public const string PartnerApi = "partner.api";
}
