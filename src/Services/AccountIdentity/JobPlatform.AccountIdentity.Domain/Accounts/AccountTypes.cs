using JobPlatform.AccountIdentity.Domain.Common;
using JobPlatform.SharedKernel.Common.Enums;
using JobPlatform.SharedKernel.Common.ValueObjects;
using JobPlatform.SharedKernel.Domain;

namespace JobPlatform.AccountIdentity.Domain.Accounts;

public enum AccountStanding
{
    Pending,
    Active,
    Banned,
    Deactivated
}

public enum RegistrationLevel
{
    Level1 = 1,
    Level2 = 2
}

public enum SuspensionKind
{
    Deactivated,
    DeletionRequested
}

public enum LoginOutcome
{
    Succeeded,
    InvalidCredentials,
    Locked,
    NotActive
}

/// <summary>Business-rule codes of the Account aggregate, format AI.Account.RULE.</summary>
public static class AccountRuleCodes
{
    public const string Duplicate = "AI.Account.DUPLICATE";
    public const string CodeInvalidOrExpired = "AI.Account.CODE_INVALID_OR_EXPIRED";
    public const string TooManyAttempts = "AI.Account.TOO_MANY_ATTEMPTS";
    public const string Banned = "AI.Account.BANNED";
    public const string AlreadyBanned = "AI.Account.ALREADY_BANNED";
    public const string AlreadyActive = "AI.Account.ALREADY_ACTIVE";
    public const string AlreadyDeactivated = "AI.Account.ALREADY_DEACTIVATED";
    public const string NotPending = "AI.Account.NOT_PENDING";
    public const string NotActive = "AI.Account.NOT_ACTIVE";
    public const string Locked = "AI.Account.LOCKED";
    public const string AdminOnly = "AI.Account.ADMIN_ONLY";
    public const string ApproverNotAuthorised = "AI.Account.APPROVER_NOT_AUTHORISED";
    public const string StaffApprovalNotApplicable = "AI.Account.STAFF_APPROVAL_NOT_APPLICABLE";
    public const string ReasonRequired = "AI.Account.REASON_REQUIRED";
    public const string MfaRequiredForAdmin = "AI.Account.MFA_REQUIRED_FOR_ADMIN";
    public const string MfaAlreadyEnrolled = "AI.Account.MFA_ALREADY_ENROLLED";
    public const string MfaNotStarted = "AI.Account.MFA_NOT_STARTED";
    public const string OtpNotApplicable = "AI.Account.OTP_NOT_APPLICABLE";
    public const string ActorTypeNotRegistrable = "AI.Account.ACTOR_TYPE_NOT_REGISTRABLE";
    public const string IdentityKeyRequired = "AI.Account.IDENTITY_KEY_REQUIRED";
    public const string EmailRequired = "AI.Account.EMAIL_REQUIRED";
    public const string EmailAlreadyVerified = "AI.Account.EMAIL_ALREADY_VERIFIED";
    public const string EmailTokenInvalid = "AI.Account.EMAIL_TOKEN_INVALID_OR_EXPIRED";
}

public sealed class PasswordHash : ValueObject
{
    public PasswordHash(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("A password hash cannot be empty.", nameof(value));
        }

        Value = value;
    }

    public string Value { get; }

    /// <summary>Never print the hash.</summary>
    public override string ToString() => "[hash]";

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }
}

/// <summary>Employer company id, or a partner's registered identity - used for duplicate detection (INV-01).</summary>
public sealed class ExternalIdentityKey : ValueObject
{
    public const int MaxLength = 128;

    private ExternalIdentityKey(string value) => Value = value;

    public string Value { get; }

    public static bool TryCreate(string? input, out ExternalIdentityKey? key)
    {
        key = null;
        if (string.IsNullOrWhiteSpace(input))
        {
            return false;
        }

        var normalised = input.Trim().ToUpperInvariant();
        if (normalised.Length > MaxLength)
        {
            return false;
        }

        key = new ExternalIdentityKey(normalised);
        return true;
    }

    public static ExternalIdentityKey Create(string input) =>
        TryCreate(input, out var key) ? key! : throw new ArgumentException("Invalid identity key.", nameof(input));

    public override string ToString() => Value;

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }
}

public sealed record RegistrationDetails(
    ActorType ActorType,
    string DisplayName,
    Email? Email,
    MobileNumber Mobile,
    ExternalIdentityKey? IdentityKey,
    RegistrationLevel RegistrationLevel = RegistrationLevel.Level1,
    int PasswordPolicyVersion = 1,
    string? RegistrationNumber = null);

public sealed record LoginAttemptResult(LoginOutcome Outcome, string? RuleCode, string? ExternalCode, DateTime? LockedUntilUtc)
{
    public bool IsSuccess => Outcome == LoginOutcome.Succeeded;

    public static LoginAttemptResult Succeeded() => new(LoginOutcome.Succeeded, null, null, null);

    public static LoginAttemptResult InvalidCredentials() =>
        new(LoginOutcome.InvalidCredentials, null, ErrorCodes.AuthInvalidCredentials, null);

    public static LoginAttemptResult Locked(DateTime until) =>
        new(LoginOutcome.Locked, AccountRuleCodes.Locked, ErrorCodes.AuthRateLimited, until);

    public static LoginAttemptResult NotActive(string ruleCode, string externalCode) =>
        new(LoginOutcome.NotActive, ruleCode, externalCode, null);
}

/// <summary>Current OTP challenge (hash + expiry, never the plain code). Updated in place on re-issue.</summary>
public sealed class ActivationChallenge
{
    private ActivationChallenge()
    {
    }

    internal ActivationChallenge(string codeHash, DateTime issuedAtUtc, DateTime expiresAtUtc)
    {
        CodeHash = codeHash;
        IssuedAtUtc = issuedAtUtc;
        ExpiresAtUtc = expiresAtUtc;
    }

    public string CodeHash { get; private set; } = string.Empty;
    public DateTime IssuedAtUtc { get; private set; }
    public DateTime ExpiresAtUtc { get; private set; }
    public int Attempts { get; private set; }

    internal void Reissue(string codeHash, DateTime issuedAtUtc, DateTime expiresAtUtc)
    {
        CodeHash = codeHash;
        IssuedAtUtc = issuedAtUtc;
        ExpiresAtUtc = expiresAtUtc;
        Attempts = 0;
    }

    internal void RegisterAttempt() => Attempts++;
}

public sealed class AccountStatusChange
{
    private AccountStatusChange()
    {
    }

    internal AccountStatusChange(AccountStanding? from, AccountStanding to, Guid by, string? reason, DateTime atUtc)
    {
        Id = Guid.NewGuid();
        From = from;
        To = to;
        By = by;
        Reason = reason;
        AtUtc = atUtc;
    }

    public Guid Id { get; private set; }
    public AccountStanding? From { get; private set; }
    public AccountStanding To { get; private set; }
    public Guid By { get; private set; }
    public string? Reason { get; private set; }
    public DateTime AtUtc { get; private set; }
}

public sealed class AccountRoleAssignment
{
    private AccountRoleAssignment()
    {
    }

    internal AccountRoleAssignment(RoleId roleId, DateTime assignedAtUtc)
    {
        RoleId = roleId;
        AssignedAtUtc = assignedAtUtc;
    }

    public RoleId RoleId { get; private set; }
    public DateTime AssignedAtUtc { get; private set; }
}

// ---------------------------------------------------------------------- domain events

public sealed record AccountRegisteredDomainEvent(AccountId AccountId, ActorType ActorType, DateTime At) : DomainEvent(At);

public sealed record AccountActivatedDomainEvent(AccountId AccountId, ActorType ActorType, Guid ActorId, DateTime At) : DomainEvent(At);

public sealed record UserAccountStandingChangedDomainEvent(
    AccountId AccountId, AccountStanding From, AccountStanding To, Guid ActorId, string? Reason, DateTime At) : DomainEvent(At);

public sealed record AccountSuspendedDomainEvent(
    AccountId AccountId, ActorType ActorType, Guid ActorId, string Reason, SuspensionKind Kind, DateTime At) : DomainEvent(At);

public sealed record CredentialsResetDomainEvent(AccountId AccountId, Guid ActorId, DateTime At) : DomainEvent(At);

public sealed record AccountRolesChangedDomainEvent(AccountId AccountId, DateTime At) : DomainEvent(At);
