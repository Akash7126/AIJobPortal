using System.Net;
using JobPlatform.AccountIdentity.Domain.Accounts;
using JobPlatform.AccountIdentity.Domain.Common;
using JobPlatform.AccountIdentity.Domain.Interfaces.Services;
using JobPlatform.SharedKernel.Common.Enums;
using JobPlatform.SharedKernel.Domain;

namespace JobPlatform.AccountIdentity.Domain.ApiCredentials;

public enum ApiCredentialStatus
{
    Active,
    Revoked,
    Expired
}

public static class ApiCredentialRuleCodes
{
    public const string PartnerNotActive = "AI.ApiCredential.PARTNER_NOT_ACTIVE";
    public const string Expired = "AI.ApiCredential.EXPIRED";
    public const string Revoked = "AI.ApiCredential.REVOKED";
    public const string AlreadyRevoked = "AI.ApiCredential.ALREADY_REVOKED";
    public const string OneActivePerPartner = "AI.ApiCredential.ONE_ACTIVE_PER_PARTNER";
    public const string IpNotAllowed = "AI.ApiCredential.IP_NOT_ALLOWED";
    public const string InvalidSecret = "AI.ApiCredential.INVALID_SECRET";
    public const string RateLimited = "AI.ApiCredential.RATE_LIMITED";
    public const string InvalidExpiry = "AI.ApiCredential.INVALID_EXPIRY";
    public const string InvalidLimits = "AI.ApiCredential.INVALID_LIMITS";
    public const string InvalidIpWhitelist = "AI.ApiCredential.INVALID_IP_WHITELIST";
}

/// <summary>Optional IP allow-list of single addresses and CIDR ranges. Empty = no IP restriction.</summary>
public sealed class IpWhitelist : ValueObject
{
    public static readonly IpWhitelist None = new(Array.Empty<string>());

    private readonly string[] _entries;

    private IpWhitelist(string[] entries) => _entries = entries;

    public IReadOnlyList<string> Entries => _entries;

    public bool IsEmpty => _entries.Length == 0;

    public static IpWhitelist Create(IEnumerable<string>? entries)
    {
        var normalised = new List<string>();
        foreach (var raw in entries ?? Array.Empty<string>())
        {
            var entry = raw?.Trim() ?? string.Empty;
            var valid = entry.Contains('/') ? IPNetwork.TryParse(entry, out _) : IPAddress.TryParse(entry, out _);
            Guard.Ensure(valid, ApiCredentialRuleCodes.InvalidIpWhitelist, $"'{entry}' is not a valid IP address or CIDR range.",
                ErrorCodes.AuthInvalidField, BusinessRuleKind.InvalidInput);
            normalised.Add(entry);
        }

        return new IpWhitelist(normalised.Distinct(StringComparer.OrdinalIgnoreCase).ToArray());
    }

    public bool Allows(string? ipAddress)
    {
        if (IsEmpty)
        {
            return true;
        }

        if (!IPAddress.TryParse(ipAddress, out var ip))
        {
            return false;
        }

        if (ip.IsIPv4MappedToIPv6)
        {
            ip = ip.MapToIPv4();
        }

        foreach (var entry in _entries)
        {
            if (entry.Contains('/'))
            {
                if (IPNetwork.TryParse(entry, out var network) && network.Contains(ip))
                {
                    return true;
                }
            }
            else if (IPAddress.TryParse(entry, out var single) && single.Equals(ip))
            {
                return true;
            }
        }

        return false;
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        foreach (var entry in _entries)
        {
            yield return entry;
        }
    }
}

public sealed class UsageLimits : ValueObject
{
    public UsageLimits(int maxRequests, int periodSeconds)
    {
        Guard.Ensure(maxRequests > 0 && periodSeconds > 0, ApiCredentialRuleCodes.InvalidLimits, "Usage limits must be greater than zero.",
            ErrorCodes.AuthInvalidField, BusinessRuleKind.InvalidInput);
        MaxRequests = maxRequests;
        PeriodSeconds = periodSeconds;
    }

    public int MaxRequests { get; }
    public int PeriodSeconds { get; }

    public static UsageLimits Default { get; } = new(AccountDefaults.DefaultCredentialMaxRequests, AccountDefaults.DefaultCredentialPeriodSeconds);

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return MaxRequests;
        yield return PeriodSeconds;
    }
}

/// <summary>IP whitelist, usage limits and expiration are each independently configurable; unset ones take platform defaults (US-3.1.3-02 AC-02).</summary>
public sealed record CredentialControls(IEnumerable<string>? IpWhitelist = null, UsageLimits? Limits = null, DateTime? ExpiresAtUtc = null);

public sealed class ApiCredential : AggregateRoot<ApiCredentialId>
{
    private ApiCredential()
    {
    }

    public AccountId PartnerAccountId { get; private set; }
    public string KeyId { get; private set; } = string.Empty;

    /// <summary>Hash of the client secret. The plaintext secret exists only in the response to the issuing call.</summary>
    public string SecretHash { get; private set; } = string.Empty;
    public ApiCredentialStatus Status { get; private set; }
    public IpWhitelist IpWhitelist { get; private set; } = IpWhitelist.None;
    public UsageLimits Limits { get; private set; } = UsageLimits.Default;
    public DateTime IssuedAtUtc { get; private set; }
    public DateTime ExpiresAtUtc { get; private set; }
    public DateTime? RevokedAtUtc { get; private set; }
    public int FailedAuthAttempts { get; private set; }
    public DateTime? FirstFailedAtUtc { get; private set; }

    public bool IsExpired(DateTime nowUtc) => nowUtc >= ExpiresAtUtc;

    /// <summary>Issues a credential. A previous active credential must already have been revoked by the caller (D-02 revoke-and-reissue).</summary>
    public static ApiCredential Issue(Account partner, string keyId, string secretHash, CredentialControls controls,
        ApiCredential? existing, Guid actorId, TimeProvider clock)
    {
        Guard.Ensure(partner.ActorType == ActorType.ExternalJobSite && partner.Standing == AccountStanding.Active,
            ApiCredentialRuleCodes.PartnerNotActive, "The partner account must be active to receive an API credential.",
            ErrorCodes.PartnerNotActive, BusinessRuleKind.Forbidden);
        Guard.Ensure(existing is null || existing.Status != ApiCredentialStatus.Active, ApiCredentialRuleCodes.OneActivePerPartner,
            "The partner already has an active credential.", ErrorCodes.AdminStateConflict, BusinessRuleKind.Conflict);

        var now = clock.GetUtcNow().UtcDateTime;
        var expiresAt = controls.ExpiresAtUtc ?? now.AddDays(AccountDefaults.DefaultCredentialLifetimeDays);
        Guard.Ensure(expiresAt > now && expiresAt <= now.AddDays(AccountDefaults.MaxCredentialLifetimeDays), ApiCredentialRuleCodes.InvalidExpiry,
            "The expiration must be in the future and within the maximum credential lifetime.", ErrorCodes.AuthInvalidField, BusinessRuleKind.InvalidInput);

        var credential = new ApiCredential
        {
            Id = ApiCredentialId.New(),
            PartnerAccountId = partner.Id,
            KeyId = keyId,
            SecretHash = secretHash,
            Status = ApiCredentialStatus.Active,
            IpWhitelist = IpWhitelist.Create(controls.IpWhitelist),
            Limits = controls.Limits ?? UsageLimits.Default,
            IssuedAtUtc = now,
            ExpiresAtUtc = expiresAt
        };
        credential.Raise(new ApiCredentialIssuedDomainEvent(credential.Id, partner.Id, actorId, expiresAt, now));
        return credential;
    }

    /// <summary>Explicit revocation; terminal.</summary>
    public void Revoke(Guid actorId, TimeProvider clock)
    {
        Guard.Ensure(Status != ApiCredentialStatus.Revoked, ApiCredentialRuleCodes.AlreadyRevoked, "The credential is already revoked.",
            ErrorCodes.AdminStateConflict, BusinessRuleKind.Conflict);
        var now = clock.GetUtcNow().UtcDateTime;
        Status = ApiCredentialStatus.Revoked;
        RevokedAtUtc = now;
        Raise(new ApiCredentialRevokedDomainEvent(Id, PartnerAccountId, actorId, now));
    }

    /// <summary>
    /// Verifies a client secret presented at the token endpoint. Failed attempts are counted before the rule fires,
    /// so the caller must persist the aggregate on failure (IPersistOnFailure). Secret is checked before expiry so an
    /// unauthenticated caller learns nothing about a credential's state.
    /// </summary>
    public void Authenticate(string secret, string? sourceIp, ISecretVerifier verifier, TimeProvider clock)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        Guard.Ensure(Status != ApiCredentialStatus.Revoked, ApiCredentialRuleCodes.Revoked, "The credential has been revoked.",
            ErrorCodes.PartnerRevoked, BusinessRuleKind.Unauthorized);

        var blocked = FailedAuthAttempts >= AccountDefaults.MaxCredentialFailedAttempts
                      && FirstFailedAtUtc is { } first && now - first < AccountDefaults.FailedLoginWindow;
        Guard.Ensure(!blocked, ApiCredentialRuleCodes.RateLimited, "Too many failed authentication attempts. Try again later.",
            ErrorCodes.ApiRateLimited, BusinessRuleKind.RateLimited);

        if (!verifier.Verify(secret, SecretHash))
        {
            if (FirstFailedAtUtc is not { } started || now - started >= AccountDefaults.FailedLoginWindow)
            {
                FirstFailedAtUtc = now;
                FailedAuthAttempts = 0;
            }

            FailedAuthAttempts++;
            throw new BusinessRuleViolationException(ApiCredentialRuleCodes.InvalidSecret, "The client credentials are invalid.",
                ErrorCodes.ApiInvalidClient, BusinessRuleKind.Unauthorized);
        }

        if (IsExpired(now))
        {
            Status = ApiCredentialStatus.Expired;
            throw new BusinessRuleViolationException(ApiCredentialRuleCodes.Expired, "The credential has expired.",
                ErrorCodes.PartnerExpired, BusinessRuleKind.Unauthorized);
        }

        Guard.Ensure(IpWhitelist.Allows(sourceIp), ApiCredentialRuleCodes.IpNotAllowed, "The request comes from an IP address that is not allowed.",
            ErrorCodes.PartnerIpNotAllowed, BusinessRuleKind.Forbidden);

        FailedAuthAttempts = 0;
        FirstFailedAtUtc = null;
    }
}

public sealed record ApiCredentialIssuedDomainEvent(ApiCredentialId ApiCredentialId, AccountId PartnerAccountId, Guid ActorId, DateTime ExpiresAtUtc, DateTime At)
    : DomainEvent(At);

public sealed record ApiCredentialRevokedDomainEvent(ApiCredentialId ApiCredentialId, AccountId PartnerAccountId, Guid ActorId, DateTime At)
    : DomainEvent(At);
