using JobPlatform.AccountIdentity.Domain.Common;
using JobPlatform.AccountIdentity.Domain.Interfaces.Services;
using JobPlatform.AccountIdentity.Domain.PasswordPolicies;
using JobPlatform.AccountIdentity.Domain.Rbac;
using JobPlatform.SharedKernel.Common.Enums;
using JobPlatform.SharedKernel.Common.ValueObjects;
using JobPlatform.SharedKernel.Domain;

namespace JobPlatform.AccountIdentity.Domain.Accounts;

/// <summary>
/// AGG-01 Account merged with AGG-12 User Account (decision D-01): one aggregate with a self-service facet
/// (register, activate, authenticate, change password) and an administrator facet (approve, ban, deactivate, reset).
/// </summary>
public sealed class Account : AggregateRoot<AccountId>
{
    private static readonly ActorType[] SelfRegistrable = { ActorType.JobSeeker, ActorType.Employer, ActorType.ExternalJobSite };

    private readonly List<AccountStatusChange> _history = new();
    private readonly List<AccountRoleAssignment> _roles = new();

    private Account()
    {
    }

    public ActorType ActorType { get; private set; }
    public string DisplayName { get; private set; } = string.Empty;
    public Email? Email { get; private set; }
    public MobileNumber Mobile { get; private set; } = default!;
    public ExternalIdentityKey? IdentityKey { get; private set; }

    /// <summary>Employer commercial registration number (Level 1 identity, US-3.1.2-01).</summary>
    public string? RegistrationNumber { get; private set; }
    public PasswordHash PasswordHash { get; private set; } = default!;
    public AccountStanding Standing { get; private set; }
    public RegistrationLevel RegistrationLevel { get; private set; }

    /// <summary>TOTP seed, encrypted at rest by Infrastructure before it reaches the aggregate.</summary>
    public string? MfaSecret { get; private set; }
    public bool MfaEnabled { get; private set; }
    public DateTime? MfaEnrolledAtUtc { get; private set; }

    /// <summary>TOTP time step of the last accepted code: a code (or any earlier one) can be used only once (replay protection).</summary>
    public long? MfaLastUsedTimeStep { get; private set; }

    public int FailedAttempts { get; private set; }
    public DateTime? FirstFailedAtUtc { get; private set; }
    public DateTime? LockedUntilUtc { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? ActivatedAtUtc { get; private set; }

    public bool MustChangePassword { get; private set; }
    public DateTime? PasswordChangedAtUtc { get; private set; }
    public int PasswordPolicyVersion { get; private set; }

    public DateTime? EmailVerifiedAtUtc { get; private set; }
    public string? EmailVerificationTokenHash { get; private set; }
    public DateTime? EmailVerificationExpiresAtUtc { get; private set; }

    public ActivationChallenge? ActivationChallenge { get; private set; }
    public IReadOnlyCollection<AccountStatusChange> StatusHistory => _history;
    public IReadOnlyCollection<AccountRoleAssignment> RoleAssignments => _roles;

    public bool MfaRequired => ActorType == ActorType.Administrator || MfaEnabled;

    public bool IsLocked(DateTime nowUtc) => LockedUntilUtc is { } until && nowUtc < until;

    /// <summary>True when the password was set under an older policy version (the new policy applies at the next change, not before).</summary>
    public bool IsBehindPasswordPolicy(PasswordPolicy policy) => PasswordPolicyVersion < policy.PolicyVersion;

    // ------------------------------------------------------------------ registration

    public static Account Register(RegistrationDetails details, PasswordHash passwordHash, TimeProvider clock)
    {
        Guard.Ensure(SelfRegistrable.Contains(details.ActorType), AccountRuleCodes.ActorTypeNotRegistrable,
            "This actor type cannot self-register.", ErrorCodes.AuthInvalidField, BusinessRuleKind.InvalidInput);
        Guard.Ensure(details.ActorType == ActorType.JobSeeker || details.IdentityKey is not null, AccountRuleCodes.IdentityKeyRequired,
            "Employers and partners must provide an identity key.", ErrorCodes.AuthInvalidField, BusinessRuleKind.InvalidInput);

        var now = Now(clock);
        var account = new Account
        {
            Id = AccountId.New(),
            ActorType = details.ActorType,
            DisplayName = details.DisplayName,
            Email = details.Email,
            Mobile = details.Mobile,
            IdentityKey = details.IdentityKey,
            RegistrationNumber = details.RegistrationNumber,
            PasswordHash = passwordHash,
            Standing = AccountStanding.Pending,
            RegistrationLevel = details.RegistrationLevel,
            CreatedAtUtc = now,
            PasswordChangedAtUtc = now,
            PasswordPolicyVersion = details.PasswordPolicyVersion
        };
        account._roles.Add(new AccountRoleAssignment(WellKnownRoles.ForActor(details.ActorType), now));
        account._history.Add(new AccountStatusChange(null, AccountStanding.Pending, account.Id.Value, "Registered", now));
        account.Raise(new AccountRegisteredDomainEvent(account.Id, details.ActorType, now));
        return account;
    }

    /// <summary>Creates an already-active administrator (bootstrap/seed; administrators do not self-register). MFA is mandatory (THR-029).</summary>
    public static Account CreateAdministrator(string displayName, Email email, MobileNumber mobile, PasswordHash passwordHash,
        int passwordPolicyVersion, TimeProvider clock)
    {
        var now = Now(clock);
        var account = new Account
        {
            Id = AccountId.New(),
            ActorType = ActorType.Administrator,
            DisplayName = displayName,
            Email = email,
            Mobile = mobile,
            PasswordHash = passwordHash,
            Standing = AccountStanding.Active,
            RegistrationLevel = RegistrationLevel.Level1,
            CreatedAtUtc = now,
            ActivatedAtUtc = now,
            PasswordChangedAtUtc = now,
            PasswordPolicyVersion = passwordPolicyVersion,
            EmailVerifiedAtUtc = now
        };
        account._roles.Add(new AccountRoleAssignment(WellKnownRoles.Administrator, now));
        account._history.Add(new AccountStatusChange(null, AccountStanding.Active, account.Id.Value, "Administrator created", now));
        return account;
    }

    // ------------------------------------------------------------------ activation (self-service)

    public void IssueActivationChallenge(string codeHash, TimeProvider clock)
    {
        Guard.Ensure(ActorType is ActorType.JobSeeker or ActorType.Employer, AccountRuleCodes.OtpNotApplicable,
            "This account type is not activated with a mobile code.", ErrorCodes.AuthInvalidField, BusinessRuleKind.BusinessRule);
        EnsurePending();

        var now = Now(clock);
        if (ActivationChallenge is null)
        {
            ActivationChallenge = new ActivationChallenge(codeHash, now, now + AccountDefaults.ActivationCodeValidity);
        }
        else
        {
            ActivationChallenge.Reissue(codeHash, now, now + AccountDefaults.ActivationCodeValidity);
        }
    }

    /// <summary>
    /// Confirms the mobile code. A wrong/expired code increments the attempt counter <b>before</b> throwing, so callers that
    /// catch the exception must still persist the aggregate (see IPersistOnFailure).
    /// </summary>
    public void Activate(string code, ISecretVerifier verifier, TimeProvider clock)
    {
        var now = Now(clock);
        EnsurePending();

        var challenge = ActivationChallenge;
        Guard.Ensure(challenge is not null, AccountRuleCodes.CodeInvalidOrExpired, "The activation code is invalid or has expired.",
            ErrorCodes.Expired(ActorType));
        Guard.Ensure(challenge!.Attempts < AccountDefaults.MaxActivationAttempts, AccountRuleCodes.TooManyAttempts,
            "Too many activation attempts. Try again later.", ErrorCodes.RateLimited(ActorType), BusinessRuleKind.RateLimited);

        challenge.RegisterAttempt();
        var valid = verifier.Verify(code, challenge.CodeHash) && now <= challenge.ExpiresAtUtc;
        Guard.Ensure(valid, AccountRuleCodes.CodeInvalidOrExpired, "The activation code is invalid or has expired.",
            ErrorCodes.Expired(ActorType));

        ActivateCore(Id.Value, now);
    }

    /// <summary>Partner onboarding approval by MoL/PEF-authorised staff (US-3.1.3-01 AC-04).</summary>
    public void ApproveByStaff(Actor approver, TimeProvider clock)
    {
        Guard.Ensure(approver.IsAuthorisedStaff, AccountRuleCodes.ApproverNotAuthorised,
            "Only MoL/PEF-authorised staff may approve partner onboarding.", ErrorCodes.AdminForbidden, BusinessRuleKind.Forbidden);
        Guard.Ensure(ActorType == ActorType.ExternalJobSite, AccountRuleCodes.StaffApprovalNotApplicable,
            "Staff approval applies to external job site accounts only.", ErrorCodes.AdminStateConflict, BusinessRuleKind.Conflict);
        EnsurePending();
        ActivateCore(approver.Id, Now(clock));
    }

    // ------------------------------------------------------------------ administrator facet

    public void ApproveByAdministrator(Actor admin, TimeProvider clock)
    {
        EnsureAdministrator(admin);
        EnsureNotBanned();
        Guard.Ensure(Standing != AccountStanding.Active, AccountRuleCodes.AlreadyActive, "The account is already active.",
            ErrorCodes.AdminStateConflict, BusinessRuleKind.Conflict);

        var now = Now(clock);
        var from = Standing;
        if (from == AccountStanding.Pending)
        {
            ActivateCore(admin.Id, now);
        }
        else
        {
            Standing = AccountStanding.Active;
            ActivatedAtUtc ??= now;
            _history.Add(new AccountStatusChange(from, AccountStanding.Active, admin.Id, "Approved by administrator", now));
        }

        Raise(new UserAccountStandingChangedDomainEvent(Id, from, AccountStanding.Active, admin.Id, "Approved by administrator", now));
    }

    public void Ban(Actor admin, string reason, TimeProvider clock)
    {
        EnsureAdministrator(admin);
        Guard.Ensure(!string.IsNullOrWhiteSpace(reason), AccountRuleCodes.ReasonRequired, "A reason is required.",
            ErrorCodes.AuthInvalidField, BusinessRuleKind.InvalidInput);
        Guard.Ensure(Standing != AccountStanding.Banned, AccountRuleCodes.AlreadyBanned, "The account is already banned.",
            ErrorCodes.AdminStateBanned, BusinessRuleKind.Conflict);

        var now = Now(clock);
        var from = Standing;
        Standing = AccountStanding.Banned;
        ActivationChallenge = null;
        _history.Add(new AccountStatusChange(from, AccountStanding.Banned, admin.Id, reason, now));
        Raise(new UserAccountStandingChangedDomainEvent(Id, from, AccountStanding.Banned, admin.Id, reason, now));
    }

    public void Deactivate(Actor admin, string reason, TimeProvider clock)
    {
        EnsureAdministrator(admin);
        Guard.Ensure(!string.IsNullOrWhiteSpace(reason), AccountRuleCodes.ReasonRequired, "A reason is required.",
            ErrorCodes.AuthInvalidField, BusinessRuleKind.InvalidInput);
        var now = Now(clock);
        DeactivateCore(admin.Id, reason, SuspensionKind.Deactivated, now);
        Raise(new UserAccountStandingChangedDomainEvent(Id, AccountStanding.Active, AccountStanding.Deactivated, admin.Id, reason, now));
    }

    /// <summary>Deactivation/deletion requested by the account owner through the profile privacy setting (US-3.1.1-07 AC-05).</summary>
    public void RequestDeactivation(SuspensionKind kind, string reason, TimeProvider clock)
    {
        Guard.Ensure(!string.IsNullOrWhiteSpace(reason), AccountRuleCodes.ReasonRequired, "A reason is required.",
            ErrorCodes.AuthInvalidField, BusinessRuleKind.InvalidInput);
        DeactivateCore(Id.Value, reason, kind, Now(clock));
    }

    /// <summary>Forces a password change at the next login and clears any lockout. Callers must invalidate live sessions.</summary>
    public void ResetCredentials(Actor admin, TimeProvider clock)
    {
        EnsureAdministrator(admin);
        var now = Now(clock);
        MustChangePassword = true;
        FailedAttempts = 0;
        FirstFailedAtUtc = null;
        LockedUntilUtc = null;
        Raise(new CredentialsResetDomainEvent(Id, admin.Id, now));
    }

    // ------------------------------------------------------------------ authentication

    /// <summary>
    /// Evaluates one login attempt. The result never distinguishes an unknown user from a wrong password (US-3.1.5-01 AC-04);
    /// failure counters are updated here and must be persisted even though the login is refused.
    /// </summary>
    public LoginAttemptResult AttemptLogin(bool passwordMatches, TimeProvider clock)
    {
        var now = Now(clock);
        if (IsLocked(now))
        {
            return LoginAttemptResult.Locked(LockedUntilUtc!.Value);
        }

        if (!passwordMatches)
        {
            return RegisterFailure(now);
        }

        // When a second factor is still pending the failure counter must survive, or a stolen password could brute-force the code.
        if (!MfaRequired)
        {
            ClearFailures();
        }

        return Standing switch
        {
            AccountStanding.Active => LoginAttemptResult.Succeeded(),
            AccountStanding.Pending => LoginAttemptResult.NotActive(AccountRuleCodes.NotActive, ErrorCodes.AuthAccountPending),
            AccountStanding.Banned => LoginAttemptResult.NotActive(AccountRuleCodes.Banned, ErrorCodes.AdminStateBanned),
            _ => LoginAttemptResult.NotActive(AccountRuleCodes.NotActive, ErrorCodes.AuthAccountDeactivated)
        };
    }

    /// <summary>Second-factor attempt: a wrong code counts toward the same lockout as a wrong password.</summary>
    public LoginAttemptResult AttemptMfa(bool codeValid, TimeProvider clock)
    {
        var now = Now(clock);
        if (IsLocked(now))
        {
            return LoginAttemptResult.Locked(LockedUntilUtc!.Value);
        }

        if (!codeValid)
        {
            return RegisterFailure(now);
        }

        ClearFailures();
        return Standing == AccountStanding.Active
            ? LoginAttemptResult.Succeeded()
            : LoginAttemptResult.NotActive(AccountRuleCodes.NotActive, ErrorCodes.AuthAccountDeactivated);
    }

    /// <summary>
    /// Second-factor attempt with replay protection: <paramref name="matchedTimeStep"/> is the TOTP time step the submitted code matched
    /// (null when it matched none). A step at or before the last accepted one is a replay and counts as a failure.
    /// </summary>
    public LoginAttemptResult AttemptMfa(long? matchedTimeStep, TimeProvider clock)
    {
        var fresh = matchedTimeStep is { } step && (MfaLastUsedTimeStep is not { } last || step > last);
        var result = AttemptMfa(fresh, clock);
        if (result.IsSuccess)
        {
            MfaLastUsedTimeStep = matchedTimeStep;
        }

        return result;
    }

    /// <summary>INV-10: MFA is mandatory for administrators (and for anyone who enrolled).</summary>
    public void EnsureMfaSatisfied(bool mfaVerified) =>
        Guard.Ensure(!MfaRequired || mfaVerified, AccountRuleCodes.MfaRequiredForAdmin, "Multi-factor authentication is required.",
            ErrorCodes.AuthMfaRequired, BusinessRuleKind.Unauthorized);

    public bool CanAuthenticate(DateTime nowUtc) => Standing == AccountStanding.Active && !IsLocked(nowUtc);

    public void ChangePassword(string newPlainPassword, PasswordHash newHash, PasswordPolicy policy, TimeProvider clock)
    {
        Guard.Ensure(Standing == AccountStanding.Active, AccountRuleCodes.NotActive, "Only active accounts can change their password.",
            ErrorCodes.AdminStateConflict, BusinessRuleKind.Conflict);
        policy.EnsureCompliant(newPlainPassword);

        PasswordHash = newHash;
        MustChangePassword = false;
        PasswordChangedAtUtc = Now(clock);
        PasswordPolicyVersion = policy.PolicyVersion;
    }

    /// <summary>
    /// Replaces the stored hash by an equivalent one made with stronger parameters after a successful verification (rehash on login).
    /// Not a password change: no policy check, no version bump, the password itself is unchanged.
    /// </summary>
    public void UpgradePasswordHash(PasswordHash stronger) => PasswordHash = stronger;

    // ------------------------------------------------------------------ MFA

    public void BeginMfaEnrollment(string protectedSecret)
    {
        Guard.Ensure(!MfaEnabled, AccountRuleCodes.MfaAlreadyEnrolled, "Multi-factor authentication is already enrolled.",
            ErrorCodes.AdminStateConflict, BusinessRuleKind.Conflict);
        MfaSecret = protectedSecret;
    }

    public void ConfirmMfaEnrollment(TimeProvider clock)
    {
        Guard.Ensure(MfaSecret is not null, AccountRuleCodes.MfaNotStarted, "Multi-factor enrolment has not been started.",
            ErrorCodes.AdminStateConflict, BusinessRuleKind.Conflict);
        if (!MfaEnabled)
        {
            MfaEnabled = true;
            MfaEnrolledAtUtc = Now(clock);
        }
    }

    // ------------------------------------------------------------------ e-mail verification

    public void IssueEmailVerification(string tokenHash, TimeProvider clock)
    {
        Guard.Ensure(Email is not null, AccountRuleCodes.EmailRequired, "The account has no e-mail address.",
            ErrorCodes.AuthInvalidField, BusinessRuleKind.InvalidInput);
        Guard.Ensure(EmailVerifiedAtUtc is null, AccountRuleCodes.EmailAlreadyVerified, "The e-mail address is already verified.",
            ErrorCodes.AdminStateConflict, BusinessRuleKind.Conflict);
        EmailVerificationTokenHash = tokenHash;
        EmailVerificationExpiresAtUtc = Now(clock) + AccountDefaults.EmailVerificationValidity;
    }

    public void VerifyEmail(string token, ISecretVerifier verifier, TimeProvider clock)
    {
        var now = Now(clock);
        var valid = EmailVerificationTokenHash is not null
                    && EmailVerificationExpiresAtUtc is { } expires && now <= expires
                    && verifier.Verify(token, EmailVerificationTokenHash);
        Guard.Ensure(valid, AccountRuleCodes.EmailTokenInvalid, "The e-mail verification token is invalid or has expired.",
            ErrorCodes.AuthInvalidField);
        EmailVerifiedAtUtc = now;
        EmailVerificationTokenHash = null;
        EmailVerificationExpiresAtUtc = null;
    }

    // ------------------------------------------------------------------ roles

    public void AssignRole(RoleId roleId, TimeProvider clock)
    {
        if (_roles.Any(r => r.RoleId == roleId))
        {
            return;
        }

        var now = Now(clock);
        _roles.Add(new AccountRoleAssignment(roleId, now));
        Raise(new AccountRolesChangedDomainEvent(Id, now));
    }

    public void RemoveRole(RoleId roleId, TimeProvider clock)
    {
        var assignment = _roles.FirstOrDefault(r => r.RoleId == roleId);
        if (assignment is null)
        {
            return;
        }

        _roles.Remove(assignment);
        Raise(new AccountRolesChangedDomainEvent(Id, Now(clock)));
    }

    // ------------------------------------------------------------------ internals

    private void ActivateCore(Guid actorId, DateTime now)
    {
        var from = Standing;
        Standing = AccountStanding.Active;
        ActivatedAtUtc = now;
        ActivationChallenge = null;
        _history.Add(new AccountStatusChange(from, AccountStanding.Active, actorId, "Activated", now));
        Raise(new AccountActivatedDomainEvent(Id, ActorType, actorId, now));
    }

    private void DeactivateCore(Guid actorId, string reason, SuspensionKind kind, DateTime now)
    {
        EnsureNotBanned();
        Guard.Ensure(Standing != AccountStanding.Deactivated, AccountRuleCodes.AlreadyDeactivated, "The account is already deactivated.",
            ErrorCodes.AdminStateConflict, BusinessRuleKind.Conflict);
        Guard.Ensure(Standing == AccountStanding.Active, AccountRuleCodes.NotActive, "Only active accounts can be deactivated.",
            ErrorCodes.AdminStateConflict, BusinessRuleKind.Conflict);

        var from = Standing;
        Standing = AccountStanding.Deactivated;
        _history.Add(new AccountStatusChange(from, AccountStanding.Deactivated, actorId, reason, now));
        Raise(new AccountSuspendedDomainEvent(Id, ActorType, actorId, reason, kind, now));
    }

    private LoginAttemptResult RegisterFailure(DateTime now)
    {
        if (FirstFailedAtUtc is not { } first || now - first >= AccountDefaults.FailedLoginWindow)
        {
            FirstFailedAtUtc = now;
            FailedAttempts = 0;
        }

        FailedAttempts++;
        if (FailedAttempts >= AccountDefaults.MaxFailedLogins)
        {
            LockedUntilUtc = now + AccountDefaults.LockDuration;
            FailedAttempts = 0;
            FirstFailedAtUtc = null;
            return LoginAttemptResult.Locked(LockedUntilUtc.Value);
        }

        return LoginAttemptResult.InvalidCredentials();
    }

    private void ClearFailures()
    {
        FailedAttempts = 0;
        FirstFailedAtUtc = null;
        LockedUntilUtc = null;
    }

    private void EnsurePending()
    {
        EnsureNotBanned();
        Guard.Ensure(Standing == AccountStanding.Pending, AccountRuleCodes.NotPending, "The account is not awaiting activation.",
            ErrorCodes.AdminStateConflict, BusinessRuleKind.Conflict);
    }

    private void EnsureNotBanned() =>
        Guard.Ensure(Standing != AccountStanding.Banned, AccountRuleCodes.Banned, "The account is banned.",
            ErrorCodes.AdminStateBanned, BusinessRuleKind.Conflict);

    private static void EnsureAdministrator(Actor actor) =>
        Guard.Ensure(actor.IsAdministrator, AccountRuleCodes.AdminOnly, "Administrator role required.",
            ErrorCodes.AdminForbidden, BusinessRuleKind.Forbidden);

    private static DateTime Now(TimeProvider clock) => clock.GetUtcNow().UtcDateTime;
}
