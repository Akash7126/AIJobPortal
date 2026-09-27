using JobPlatform.AccountIdentity.Domain.Accounts;
using JobPlatform.AccountIdentity.Domain.Common;
using JobPlatform.AccountIdentity.Domain.Rbac;
using JobPlatform.AccountIdentity.Domain.Sessions;
using JobPlatform.SharedKernel.ApiContracts.AccountIdentity;
using JobPlatform.SharedKernel.Application.Paging;
using JobPlatform.SharedKernel.Common.Enums;
using JobPlatform.SharedKernel.Common.ValueObjects;

namespace JobPlatform.AccountIdentity.Application.Abstractions;

/// <summary>Adaptive one-way password hashing (Q-05: strong hashing, not "AES-256").</summary>
public interface IPasswordHasher : ISecretVerifier
{
    string Hash(string password);

    /// <summary>True when the stored hash uses weaker parameters than the current configuration (rehash on next login).</summary>
    bool NeedsRehash(string hash);

    /// <summary>Spends the same effort as a real verification. Used for unknown accounts so timing does not reveal whether a login exists.</summary>
    void SimulateVerify(string password);
}

/// <summary>Keyed hash for short-lived one-time codes (activation OTP, e-mail codes).</summary>
public interface IOtpHasher : ISecretVerifier
{
    string Hash(string code);
}

/// <summary>Keyed hash for high-entropy random secrets (API secrets, refresh tokens, verification tokens).</summary>
public interface IApiSecretHasher : ISecretVerifier
{
    string Hash(string secret);
}

public interface ISecretGenerator
{
    /// <summary>Six decimal digits from a cryptographic RNG.</summary>
    string GenerateOtp();

    string GenerateApiKeyId();

    string GenerateApiSecret();

    /// <summary>URL-safe random token (refresh secret, e-mail verification token, MFA challenge).</summary>
    string GenerateToken();
}

/// <summary>Delivers the mobile activation code. Q-03: BC-13 does not yet consume an OTP request, so this port has a dev/log adapter.</summary>
public interface IOtpSender
{
    Task SendActivationCodeAsync(MobileNumber mobile, string code, Language language, CancellationToken ct = default);
}

public interface IEmailVerificationSender
{
    Task SendVerificationAsync(Email email, Guid accountId, string token, Language language, CancellationToken ct = default);

    Task SendLoginCodeAsync(Email email, string code, Language language, CancellationToken ct = default);
}

/// <summary>TOTP (RFC 6238) for multi-factor authentication (Q-08: TOTP). The seed is encrypted at rest (AES-256-GCM).</summary>
public interface IMfaService
{
    string GenerateSecret();

    string Protect(string secret);

    string Unprotect(string protectedSecret);

    string BuildProvisioningUri(string issuer, string accountLabel, string secret);

    /// <summary>Returns the TOTP time step the code matches (current step +/- 1 for clock drift), or null. The caller rejects steps it has already accepted.</summary>
    long? FindMatchingTimeStep(string secret, string code, DateTime nowUtc);
}

public sealed record UserTokenRequest(Guid AccountId, ActorType ActorType, IReadOnlyCollection<Guid> RoleIds, Guid SessionId, bool MfaVerified,
    bool MustChangePassword);

public sealed record ClientTokenRequest(Guid PartnerAccountId, string ClientId, IReadOnlyCollection<string> Scopes, IReadOnlyCollection<Guid> RoleIds,
    ActorType ActorType, TimeSpan Lifetime);

public sealed record IssuedAccessToken(string Token, string TokenId, DateTime ExpiresAtUtc);

/// <summary>JWT issuing with asymmetric signing keys published through JWKS so every BC validates tokens locally.</summary>
public interface IAccessTokenService
{
    TimeSpan UserTokenLifetime { get; }

    IssuedAccessToken IssueUserToken(UserTokenRequest request);

    IssuedAccessToken IssueClientToken(ClientTokenRequest request);
}

/// <summary>Redis-backed session state: sliding idle timeout, refresh-token rotation, immediate invalidation.</summary>
public interface ISessionStore
{
    Task CreateAsync(UserSession session, CancellationToken ct = default);

    Task<UserSession?> GetAsync(Guid sessionId, CancellationToken ct = default);

    /// <summary>Persists a changed session (touch, refresh rotation, invalidation) and re-arms its idle TTL.</summary>
    Task SaveAsync(UserSession session, CancellationToken ct = default);

    Task InvalidateAllForAccountAsync(Guid accountId, Guid? exceptSessionId = null, CancellationToken ct = default);

    Task RevokeTokenAsync(string tokenId, TimeSpan ttl, CancellationToken ct = default);

    Task<bool> IsTokenRevokedAsync(string tokenId, CancellationToken ct = default);

    Task RevokeClientAsync(string clientId, TimeSpan ttl, CancellationToken ct = default);

    Task<bool> IsClientRevokedAsync(string clientId, CancellationToken ct = default);
}

public sealed record MfaChallengeToken(string Token, DateTime ExpiresAtUtc);

/// <summary>Short-lived proof that the password step succeeded and the second factor is pending.</summary>
public interface IMfaChallengeStore
{
    Task<MfaChallengeToken> CreateAsync(Guid accountId, CancellationToken ct = default);

    Task<Guid?> GetAccountIdAsync(string token, CancellationToken ct = default);

    Task DeleteAsync(string token, CancellationToken ct = default);
}

/// <summary>One-time codes for the e-mail verification login mechanism (stored hashed, capped attempts, TTL).</summary>
public interface ILoginCodeStore
{
    Task StoreAsync(Guid accountId, string codeHash, TimeSpan ttl, CancellationToken ct = default);

    Task<bool> VerifyAndConsumeAsync(Guid accountId, string code, ISecretVerifier verifier, int maxAttempts, CancellationToken ct = default);
}

public sealed record AccessLogEntry(DateTime AtUtc, Guid? AccountId, string Action, string Resource, string Decision, string? Reason, string? IpAddress);

/// <summary>Appends authentication/authorisation decisions (US-3.1.5-03 AC-03). Never receives passwords, tokens or codes.</summary>
public interface IAccessLog
{
    Task AppendAsync(AccessLogEntry entry, CancellationToken ct = default);
}

/// <summary>Role-to-permission map with immediate invalidation (Redis cache-aside, D-03: tokens carry role ids only).</summary>
public interface IRoleDirectory
{
    Task<IReadOnlyList<RolePermissions>> GetRolesForAccountAsync(Guid accountId, CancellationToken ct = default);
}

public interface ICacheInvalidator
{
    Task InvalidateRoleMapAsync(CancellationToken ct = default);

    Task InvalidateAccountRolesAsync(Guid accountId, CancellationToken ct = default);

    Task InvalidatePasswordPolicyAsync(CancellationToken ct = default);
}

public sealed record ServiceClient(string ClientId, IReadOnlyCollection<string> Scopes);

/// <summary>Internal service clients (other BCs) that obtain tokens with client-credentials for /internal/v1.</summary>
public interface IServiceClientRegistry
{
    ServiceClient? Authenticate(string clientId, string clientSecret);
}

public sealed class ConsentOptions
{
    public const string SectionName = "Consent";

    public string CurrentPolicyVersion { get; set; } = "2026-01";
    public string PrivacyPolicyUrl { get; set; } = "/privacy-policy";
    public string BannerTextEn { get; set; } = "We use essential cookies to run the platform. With your consent we also use analytics, preference and marketing cookies. See our privacy policy.";
    public string BannerTextAr { get; set; } = "نستخدم ملفات تعريف الارتباط الأساسية لتشغيل المنصة. وبموافقتك نستخدم أيضاً ملفات التحليلات والتفضيلات والتسويق. اطلع على سياسة الخصوصية.";
}

// ------------------------------------------------------------------ read side (projections, never aggregates)

public sealed record AccountStatusChangeView(string? From, string To, Guid By, string? Reason, DateTime AtUtc);

public sealed record AccountStandingView(
    Guid AccountId,
    ActorType ActorType,
    string DisplayName,
    string? Email,
    string Mobile,
    string Standing,
    bool IsLocked,
    DateTime? LockedUntilUtc,
    bool MfaEnabled,
    bool EmailVerified,
    bool MustChangePassword,
    DateTime CreatedAtUtc,
    DateTime? ActivatedAtUtc,
    IReadOnlyList<AccountStatusChangeView> History,
    IReadOnlyList<string> AvailableActions,
    string ETag = "");

public sealed record AccountListItemView(Guid AccountId, ActorType ActorType, string DisplayName, string? Email, string Mobile, string Standing,
    DateTime CreatedAtUtc);

public sealed record AccountListFilter(ActorType? ActorType, string? Standing, string? Search);

public sealed record PasswordPolicyView(int MinLength, bool RequireUpper, bool RequireLower, bool RequireDigit, int PolicyVersion, DateTime UpdatedAtUtc,
    string ETag = "");

public sealed record SessionTimeoutView(int IdleTimeoutMinutes, int SettingVersion, DateTime UpdatedAtUtc, string ETag = "");

/// <param name="ETag">Strong tag of the role (RowVersion): send it as If-Match when changing the role. Lists have no single ETag header, so it travels in the body.</param>
public sealed record RoleView(Guid RoleId, string Name, bool IsSystem, IReadOnlyList<string> Permissions, string ETag = "");

public sealed record ApiCredentialView(Guid ApiCredentialId, string KeyId, string Status, IReadOnlyList<string> IpWhitelist, int MaxRequests,
    int PeriodSeconds, DateTime IssuedAtUtc, DateTime ExpiresAtUtc);

public sealed record ConsentDecisionView(Guid GuestId, string PolicyVersion, bool Analytics, bool Preferences, bool Marketing, DateTime DecidedAtUtc,
    string Locale);

public interface IIdentityReadStore
{
    Task<AccountSummaryDto?> GetAccountSummaryAsync(Guid accountId, CancellationToken ct = default);

    Task<AccountStandingView?> GetAccountStandingAsync(Guid accountId, CancellationToken ct = default);

    Task<PagedResult<AccountListItemView>> ListAccountsAsync(AccountListFilter filter, PageRequest page, CancellationToken ct = default);

    Task<ApiCredentialControlsDto?> GetApiCredentialControlsAsync(Guid apiCredentialId, CancellationToken ct = default);

    Task<ApiCredentialView?> GetActiveApiCredentialForPartnerAsync(Guid partnerAccountId, CancellationToken ct = default);

    Task<PagedResult<AccessLogEntryDto>> ListAccessLogAsync(DateTime? fromUtc, DateTime? toUtc, Guid? accountId, PageRequest page, CancellationToken ct = default);

    Task<PasswordPolicyView> GetPasswordPolicyAsync(CancellationToken ct = default);

    Task<SessionTimeoutView> GetSessionTimeoutAsync(CancellationToken ct = default);

    Task<IReadOnlyList<RoleView>> ListRolesAsync(CancellationToken ct = default);

    Task<ConsentDecisionView?> GetConsentAsync(Guid guestId, string policyVersion, CancellationToken ct = default);
}
