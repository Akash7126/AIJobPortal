using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using JobPlatform.AccountIdentity.Application.Abstractions;
using JobPlatform.AccountIdentity.Infrastructure.Persistence;
using JobPlatform.SharedKernel.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace JobPlatform.AccountIdentity.Infrastructure.Security;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = "https://identity.jobplatform.local";
    public string Audience { get; set; } = "jobplatform";
    public int AccessTokenMinutes { get; set; } = 15;
    public int SigningKeyLifetimeDays { get; set; } = 90;
    public int SigningKeyRotateBeforeDays { get; set; } = 14;
}

/// <summary>
/// Owns the asymmetric signing keys (RS256). The current key signs; every non-expired key is published through JWKS so other BCs
/// validate tokens locally and keep working across rotation. Private keys are stored AES-256-GCM encrypted.
/// </summary>
public sealed class SigningKeyService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly AesGcmProtector _protector;
    private readonly JwtOptions _options;
    private readonly TimeProvider _clock;
    private readonly ILogger<SigningKeyService> _logger;
    private volatile KeyState? _state;

    public SigningKeyService(IServiceScopeFactory scopes, AesGcmProtector protector, IOptions<JwtOptions> options, TimeProvider clock,
        ILogger<SigningKeyService> logger)
    {
        _scopes = scopes;
        _protector = protector;
        _options = options.Value;
        _clock = clock;
        _logger = logger;
    }

    /// <summary>Loads the keys, creating a new signing key when none is valid or the current one is close to expiry.</summary>
    public async Task EnsureActiveKeyAsync(CancellationToken ct = default)
    {
        using var scope = _scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var now = _clock.GetUtcNow().UtcDateTime;

        var keys = await db.SigningKeys.Where(k => k.NotAfterUtc > now.AddDays(-1)).ToListAsync(ct);
        var current = keys.Where(k => k.NotBeforeUtc <= now && k.NotAfterUtc > now).OrderByDescending(k => k.NotBeforeUtc).FirstOrDefault();
        if (current is null || current.NotAfterUtc - now < TimeSpan.FromDays(_options.SigningKeyRotateBeforeDays))
        {
            var created = CreateKey(now);
            db.SigningKeys.Add(created);
            await db.SaveChangesAsync(ct);
            keys.Add(created);
            _logger.LogInformation("Created signing key {Kid} valid until {NotAfter:u}", created.Kid, created.NotAfterUtc);
        }

        _state = BuildState(keys, now);
    }

    public SigningCredentials GetSigningCredentials() =>
        (_state ?? throw new InvalidOperationException("Signing keys are not loaded yet.")).Current;

    public IReadOnlyCollection<SecurityKey> GetValidationKeys() =>
        (_state ?? throw new InvalidOperationException("Signing keys are not loaded yet.")).Validation;

    /// <summary>The public keys as a JWKS document (RFC 7517).</summary>
    public string GetJwksJson()
    {
        var keys = (_state ?? throw new InvalidOperationException("Signing keys are not loaded yet.")).Validation
            .OfType<RsaSecurityKey>()
            .Select(k => new
            {
                kty = "RSA",
                use = "sig",
                alg = SecurityAlgorithms.RsaSha256,
                kid = k.KeyId,
                n = Base64UrlEncoder.Encode(k.Parameters.Modulus),
                e = Base64UrlEncoder.Encode(k.Parameters.Exponent)
            });
        return JsonSerializer.Serialize(new { keys });
    }

    private SigningKeyRecord CreateKey(DateTime now)
    {
        using var rsa = RSA.Create(2048);
        return new SigningKeyRecord
        {
            Kid = Guid.NewGuid().ToString("N"),
            PublicKey = Convert.ToBase64String(rsa.ExportSubjectPublicKeyInfo()),
            EncryptedPrivateKey = _protector.Protect(rsa.ExportPkcs8PrivateKey()),
            NotBeforeUtc = now,
            NotAfterUtc = now.AddDays(_options.SigningKeyLifetimeDays)
        };
    }

    private KeyState BuildState(IEnumerable<SigningKeyRecord> records, DateTime now)
    {
        var validation = new List<SecurityKey>();
        SigningCredentials? current = null;
        DateTime currentNotBefore = DateTime.MinValue;
        foreach (var record in records.OrderBy(r => r.NotBeforeUtc))
        {
            using var publicRsa = RSA.Create();
            publicRsa.ImportSubjectPublicKeyInfo(Convert.FromBase64String(record.PublicKey), out _);
            validation.Add(new RsaSecurityKey(publicRsa.ExportParameters(false)) { KeyId = record.Kid });

            if (record.NotBeforeUtc <= now && record.NotAfterUtc > now && record.NotBeforeUtc >= currentNotBefore)
            {
                var privateRsa = RSA.Create();
                privateRsa.ImportPkcs8PrivateKey(_protector.Unprotect(record.EncryptedPrivateKey), out _);
                current = new SigningCredentials(new RsaSecurityKey(privateRsa) { KeyId = record.Kid }, SecurityAlgorithms.RsaSha256)
                {
                    CryptoProviderFactory = new CryptoProviderFactory { CacheSignatureProviders = false }
                };
                currentNotBefore = record.NotBeforeUtc;
            }
        }

        return new KeyState(current ?? throw new InvalidOperationException("No valid signing key."), validation);
    }

    private sealed record KeyState(SigningCredentials Current, IReadOnlyCollection<SecurityKey> Validation);
}

/// <summary>Hourly check that a valid signing key exists and rotates it ahead of expiry.</summary>
public sealed class SigningKeyRotationService : BackgroundService
{
    private readonly SigningKeyService _keys;
    private readonly ILogger<SigningKeyRotationService> _logger;

    public SigningKeyRotationService(SigningKeyService keys, ILogger<SigningKeyRotationService> logger)
    {
        _keys = keys;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromHours(1));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await _keys.EnsureActiveKeyAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Signing key rotation check failed");
            }
        }
    }
}

/// <summary>Issues RS256 JWTs. User tokens carry role ids only (D-03); permissions are resolved per request from the cached role map.</summary>
public sealed class JwtAccessTokenService : IAccessTokenService
{
    private readonly SigningKeyService _keys;
    private readonly JwtOptions _options;
    private readonly TimeProvider _clock;
    private readonly JsonWebTokenHandler _handler = new();

    public JwtAccessTokenService(SigningKeyService keys, IOptions<JwtOptions> options, TimeProvider clock)
    {
        _keys = keys;
        _options = options.Value;
        _clock = clock;
    }

    public TimeSpan UserTokenLifetime => TimeSpan.FromMinutes(_options.AccessTokenMinutes);

    public IssuedAccessToken IssueUserToken(UserTokenRequest request)
    {
        var claims = new List<Claim>
        {
            new(AppClaimTypes.ActorType, request.ActorType.ToString()),
            new(AppClaimTypes.SessionId, request.SessionId.ToString())
        };
        claims.AddRange(request.RoleIds.Select(r => new Claim(AppClaimTypes.RoleId, r.ToString())));
        if (request.MfaVerified)
        {
            claims.Add(new Claim(AppClaimTypes.Amr, AppClaimTypes.MfaValue));
        }

        if (request.MustChangePassword)
        {
            claims.Add(new Claim(AppClaimTypes.MustChangePassword, "true"));
        }

        return Create(request.AccountId.ToString(), claims, UserTokenLifetime);
    }

    public IssuedAccessToken IssueClientToken(ClientTokenRequest request)
    {
        var claims = new List<Claim>
        {
            new(AppClaimTypes.ActorType, request.ActorType.ToString()),
            new(AppClaimTypes.ClientId, request.ClientId),
            new(AppClaimTypes.Scope, string.Join(' ', request.Scopes))
        };
        claims.AddRange(request.RoleIds.Select(r => new Claim(AppClaimTypes.RoleId, r.ToString())));
        var subject = request.PartnerAccountId == Guid.Empty ? request.ClientId : request.PartnerAccountId.ToString();
        return Create(subject, claims, request.Lifetime);
    }

    private IssuedAccessToken Create(string subject, IEnumerable<Claim> claims, TimeSpan lifetime)
    {
        var now = _clock.GetUtcNow().UtcDateTime;
        var jti = Guid.NewGuid().ToString("N");
        var expires = now + lifetime;
        var allClaims = new List<Claim> { new(JwtRegisteredClaimNames.Sub, subject), new(JwtRegisteredClaimNames.Jti, jti) };
        allClaims.AddRange(claims);

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = _options.Issuer,
            Audience = _options.Audience,
            Subject = new ClaimsIdentity(allClaims),
            NotBefore = now,
            IssuedAt = now,
            Expires = expires,
            SigningCredentials = _keys.GetSigningCredentials()
        };
        return new IssuedAccessToken(_handler.CreateToken(descriptor), jti, expires);
    }
}

/// <summary>Other BCs and partners' servers that obtain client-credentials tokens for /internal/v1. Secrets come from configuration (secret store).</summary>
public sealed class ServiceClientOptions
{
    public const string SectionName = "ServiceClients";

    public List<ServiceClientEntry> Clients { get; set; } = new();
}

public sealed class ServiceClientEntry
{
    public string ClientId { get; set; } = string.Empty;
    public string Secret { get; set; } = string.Empty;
    public List<string> Scopes { get; set; } = new() { Scopes_Internal };

    private const string Scopes_Internal = SharedKernel.Security.Scopes.Internal;
}

public sealed class ConfiguredServiceClientRegistry : IServiceClientRegistry
{
    private readonly IReadOnlyList<ServiceClientEntry> _clients;

    public ConfiguredServiceClientRegistry(IOptions<ServiceClientOptions> options) =>
        _clients = options.Value.Clients.Where(c => c.ClientId.Length > 0 && c.Secret.Length > 0).ToList();

    public ServiceClient? Authenticate(string clientId, string clientSecret)
    {
        var supplied = SHA256.HashData(Encoding.UTF8.GetBytes(clientSecret));
        ServiceClient? match = null;
        foreach (var client in _clients)
        {
            var same = CryptographicOperations.FixedTimeEquals(SHA256.HashData(Encoding.UTF8.GetBytes(client.Secret)), supplied);
            if (same && string.Equals(client.ClientId, clientId, StringComparison.Ordinal))
            {
                match = new ServiceClient(client.ClientId, client.Scopes);
            }
        }

        return match;
    }
}
