using JobPlatform.AccountIdentity.Domain.Accounts;
using JobPlatform.AccountIdentity.Domain.ApiCredentials;
using JobPlatform.AccountIdentity.Domain.Consent;
using JobPlatform.AccountIdentity.Domain.PasswordPolicies;
using JobPlatform.AccountIdentity.Domain.Rbac;
using JobPlatform.AccountIdentity.Domain.Sessions;
using JobPlatform.AccountIdentity.Infrastructure.Persistence.Configurations;
using JobPlatform.BuildingBlocks.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace JobPlatform.AccountIdentity.Infrastructure.Persistence;

public sealed class IdentityDbContext : BaseDbContext
{
    public const string Schema = "identity";

    public IdentityDbContext(DbContextOptions<IdentityDbContext> options) : base(options)
    {
    }

    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<ApiCredential> ApiCredentials => Set<ApiCredential>();
    public DbSet<PasswordPolicy> PasswordPolicies => Set<PasswordPolicy>();
    public DbSet<SessionTimeoutSetting> SessionSettings => Set<SessionTimeoutSetting>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<PrivacyConsent> PrivacyConsents => Set<PrivacyConsent>();
    public DbSet<AccessLogRecord> AccessLog => Set<AccessLogRecord>();
    public DbSet<SigningKeyRecord> SigningKeys => Set<SigningKeyRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfiguration(new AccountConfiguration(IsSqlite));
        modelBuilder.ApplyConfiguration(new ApiCredentialConfiguration(IsSqlite));
        modelBuilder.ApplyConfiguration(new PasswordPolicyConfiguration(IsSqlite));
        modelBuilder.ApplyConfiguration(new SessionTimeoutSettingConfiguration(IsSqlite));
        modelBuilder.ApplyConfiguration(new RoleConfiguration(IsSqlite));
        modelBuilder.ApplyConfiguration(new PrivacyConsentConfiguration(IsSqlite));
        modelBuilder.ApplyConfiguration(new AccessLogRecordConfiguration());
        modelBuilder.ApplyConfiguration(new SigningKeyRecordConfiguration());
        base.OnModelCreating(modelBuilder);
    }
}

/// <summary>identity.AccessLog row: authentication/authorisation decisions. Never holds passwords, tokens or codes.</summary>
public sealed class AccessLogRecord
{
    public Guid Id { get; set; }
    public DateTime AtUtc { get; set; }
    public Guid? AccountId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string Resource { get; set; } = string.Empty;
    public string Decision { get; set; } = string.Empty;
    public string? Reason { get; set; }
    public string? IpAddress { get; set; }
}

/// <summary>identity.SigningKeys row: RSA key pair for JWT signing; the private key is stored encrypted (AES-256-GCM).</summary>
public sealed class SigningKeyRecord
{
    public string Kid { get; set; } = string.Empty;
    public string PublicKey { get; set; } = string.Empty;
    public string EncryptedPrivateKey { get; set; } = string.Empty;
    public DateTime NotBeforeUtc { get; set; }
    public DateTime NotAfterUtc { get; set; }
}
