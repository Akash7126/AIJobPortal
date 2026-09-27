using JobPlatform.SharedKernel.Common.Enums;
using JobPlatform.SharedKernel.Domain;

namespace JobPlatform.AccountIdentity.Domain.Common;

public readonly record struct AccountId(Guid Value)
{
    public static AccountId New() => new(Guid.NewGuid());
    public override string ToString() => Value.ToString();
}

public readonly record struct ApiCredentialId(Guid Value)
{
    public static ApiCredentialId New() => new(Guid.NewGuid());
    public override string ToString() => Value.ToString();
}

public readonly record struct RoleId(Guid Value)
{
    public static RoleId New() => new(Guid.NewGuid());
    public override string ToString() => Value.ToString();
}

/// <summary>Verifies a plaintext secret (password, OTP, API secret) against its stored hash. Implemented in Infrastructure; keeps crypto out of the domain.</summary>
public interface ISecretVerifier
{
    bool Verify(string secret, string hash);
}

/// <summary>The caller performing an action, as the domain needs to know it. Built by the application layer from the authenticated user.</summary>
public sealed record Actor(Guid Id, bool IsAdministrator, bool IsAuthorisedStaff)
{
    public static Actor Administrator(Guid id) => new(id, true, false);

    /// <summary>MoL/PEF-authorised staff (an administrator holding the approve-partner permission, Q-07).</summary>
    public static Actor AuthorisedStaff(Guid id) => new(id, true, true);

    public static Actor NonPrivileged(Guid id) => new(id, false, false);
}

/// <summary>Platform defaults (assumption A-01) - every value is confirmable with the Ministry.</summary>
public static class AccountDefaults
{
    public static readonly TimeSpan ActivationCodeValidity = TimeSpan.FromMinutes(10);
    public const int MaxActivationAttempts = 5;
    public const int MaxFailedLogins = 5;
    public static readonly TimeSpan FailedLoginWindow = TimeSpan.FromMinutes(15);
    public static readonly TimeSpan LockDuration = TimeSpan.FromMinutes(15);
    public static readonly TimeSpan EmailVerificationValidity = TimeSpan.FromHours(24);
    public const int DefaultIdleTimeoutMinutes = 30;
    public const int MinIdleTimeoutMinutes = 5;
    public const int MaxIdleTimeoutMinutes = 480;
    public const int DefaultPasswordMinLength = 8;
    public const int MaxPasswordLength = 128;
    public const int DefaultCredentialLifetimeDays = 365;
    public const int MaxCredentialLifetimeDays = 730;
    public const int DefaultCredentialMaxRequests = 1000;
    public const int DefaultCredentialPeriodSeconds = 3600;
    public const int MaxCredentialFailedAttempts = 5;
}

/// <summary>Externally published error codes (handover section 4).</summary>
public static class ErrorCodes
{
    public const string JobSeekerDuplicate = "E-JSRPM-DUPLICATE";
    public const string JobSeekerExpired = "E-JSRPM-EXPIRED";
    public const string JobSeekerRateLimited = "E-JSRPM-RATE-LIMITED";
    public const string EmployerDuplicate = "E-ERPM-DUPLICATE";
    public const string EmployerExpired = "E-ERPM-EXPIRED";
    public const string EmployerRateLimited = "E-ERPM-RATE-LIMITED";
    public const string PartnerDuplicate = "E-TPJPRI-DUPLICATE";
    public const string PartnerExpired = "E-TPJPRI-EXPIRED";
    public const string PartnerRevoked = "E-TPJPRI-REVOKED";
    public const string PartnerNotActive = "E-TPJPRI-PARTNER-NOT-ACTIVE";
    public const string PartnerIpNotAllowed = "E-TPJPRI-IP-NOT-ALLOWED";
    public const string AuthRateLimited = "E-AAFR-RATE-LIMITED";
    public const string AuthInvalidField = "E-AAFR-INVALID-FIELD";
    public const string AuthForbidden = "E-AAFR-FORBIDDEN";
    public const string AuthUnauthorized = "E-AAFR-UNAUTHORIZED";
    public const string AuthInvalidCredentials = "E-AAFR-INVALID-CREDENTIALS";
    public const string AuthAccountPending = "E-AAFR-ACCOUNT-PENDING";
    public const string AuthAccountDeactivated = "E-AAFR-ACCOUNT-DEACTIVATED";
    public const string AuthSessionExpired = "E-AAFR-SESSION-EXPIRED";
    public const string AuthMfaRequired = "E-AAFR-MFA-REQUIRED";
    public const string AdminStateBanned = "E-AUM-STATE-BANNED";
    public const string AdminForbidden = "E-AUM-FORBIDDEN";
    public const string AdminStateConflict = "E-AUM-STATE-CONFLICT";
    public const string ApiExpired = "E-APIF-EXPIRED";
    public const string ApiRateLimited = "E-APIF-RATE-LIMITED";
    public const string ApiInvalidClient = "E-APIF-INVALID-CLIENT";
    public const string NotFound = "E-ACCOUNT-NOT-FOUND";

    public static string Duplicate(ActorType actorType) => actorType switch
    {
        ActorType.JobSeeker => JobSeekerDuplicate,
        ActorType.Employer => EmployerDuplicate,
        ActorType.ExternalJobSite => PartnerDuplicate,
        _ => AuthInvalidField
    };

    public static string Expired(ActorType actorType) => actorType switch
    {
        ActorType.Employer => EmployerExpired,
        ActorType.ExternalJobSite => PartnerExpired,
        _ => JobSeekerExpired
    };

    public static string RateLimited(ActorType actorType) => actorType switch
    {
        ActorType.JobSeeker => JobSeekerRateLimited,
        ActorType.Employer => EmployerRateLimited,
        _ => AuthRateLimited
    };
}

/// <summary>Repository helpers shared by aggregates for enforcing rules with one call.</summary>
internal static class Guard
{
    public static void Ensure(bool condition, string code, string message, string? externalCode = null,
        BusinessRuleKind kind = BusinessRuleKind.BusinessRule)
    {
        if (!condition)
        {
            throw new BusinessRuleViolationException(code, message, externalCode, kind);
        }
    }
}
