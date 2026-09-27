using System.Text.Json;
using JobPlatform.AccountIdentity.Domain.Accounts;
using JobPlatform.AccountIdentity.Domain.ApiCredentials;
using JobPlatform.AccountIdentity.Domain.Common;
using JobPlatform.AccountIdentity.Domain.Consent;
using JobPlatform.AccountIdentity.Domain.PasswordPolicies;
using JobPlatform.AccountIdentity.Domain.Rbac;
using JobPlatform.AccountIdentity.Domain.Sessions;
using JobPlatform.BuildingBlocks.Infrastructure.Persistence;
using JobPlatform.SharedKernel.Common.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JobPlatform.AccountIdentity.Infrastructure.Persistence.Configurations;

/// <summary>Mapping only (value converters, indexes, constraints). Business rules stay in the domain.</summary>
internal sealed class AccountConfiguration : IEntityTypeConfiguration<Account>
{
    private readonly bool _isSqlite;

    public AccountConfiguration(bool isSqlite) => _isSqlite = isSqlite;

    public void Configure(EntityTypeBuilder<Account> builder)
    {
        builder.ToTable("Accounts", IdentityDbContext.Schema);
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).HasConversion(id => id.Value, v => new AccountId(v)).ValueGeneratedNever();
        builder.ConfigureAggregate(_isSqlite);

        builder.Property(a => a.ActorType).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(a => a.DisplayName).HasMaxLength(200).IsRequired();
        builder.Property(a => a.Email).HasConversion(e => e!.Value, s => Email.Create(s)).HasMaxLength(254);
        builder.Property(a => a.Mobile).HasConversion(m => m.Value, s => MobileNumber.Create(s)).HasMaxLength(20).IsRequired();
        builder.Property(a => a.IdentityKey).HasConversion(k => k!.Value, s => ExternalIdentityKey.Create(s)).HasMaxLength(128)
            .HasColumnName("ExternalIdentityKey");
        builder.Property(a => a.RegistrationNumber).HasMaxLength(64);
        builder.Property(a => a.PasswordHash).HasConversion(h => h.Value, s => new PasswordHash(s)).HasMaxLength(512).IsRequired();
        builder.Property(a => a.Standing).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(a => a.RegistrationLevel).HasConversion<int>();
        builder.Property(a => a.MfaSecret).HasMaxLength(512);
        builder.Property(a => a.EmailVerificationTokenHash).HasMaxLength(256);

        // INV-01 (final guard behind IAccountUniquenessChecker): one account per actor type and mobile / e-mail / identity key.
        builder.HasIndex(a => new { a.ActorType, a.Mobile }).IsUnique().HasDatabaseName("UQ_Accounts_ActorType_Mobile");
        builder.HasIndex(a => new { a.ActorType, a.Email }).IsUnique().HasFilter("[Email] IS NOT NULL").HasDatabaseName("UQ_Accounts_ActorType_Email");
        builder.HasIndex(a => new { a.ActorType, a.IdentityKey }).IsUnique().HasFilter("[ExternalIdentityKey] IS NOT NULL")
            .HasDatabaseName("UQ_Accounts_ActorType_ExternalIdentityKey");

        builder.OwnsOne(a => a.ActivationChallenge, challenge =>
        {
            challenge.ToTable("ActivationChallenges", IdentityDbContext.Schema);
            challenge.WithOwner().HasForeignKey("AccountId");
            challenge.HasKey("AccountId");
            challenge.Property(c => c.CodeHash).HasMaxLength(256).IsRequired();
        });

        builder.OwnsMany(a => a.StatusHistory, history =>
        {
            history.ToTable("AccountStatusHistory", IdentityDbContext.Schema);
            history.WithOwner().HasForeignKey("AccountId");
            history.HasKey(h => h.Id);
            history.Property(h => h.Id).ValueGeneratedNever();
            history.Property(h => h.From).HasConversion<string>().HasMaxLength(20);
            history.Property(h => h.To).HasConversion<string>().HasMaxLength(20).IsRequired();
            history.Property(h => h.Reason).HasMaxLength(500);
            history.HasIndex("AccountId", nameof(AccountStatusChange.AtUtc)).HasDatabaseName("IX_AccountStatusHistory_AccountId_AtUtc");
        });
        builder.Navigation(a => a.StatusHistory).HasField("_history").UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.OwnsMany(a => a.RoleAssignments, roles =>
        {
            roles.ToTable("AccountRoles", IdentityDbContext.Schema);
            roles.WithOwner().HasForeignKey("AccountId");
            roles.Property(r => r.RoleId).HasConversion(id => id.Value, v => new RoleId(v));
            roles.HasKey("AccountId", nameof(AccountRoleAssignment.RoleId));
        });
        builder.Navigation(a => a.RoleAssignments).HasField("_roles").UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class ApiCredentialConfiguration : IEntityTypeConfiguration<ApiCredential>
{
    private readonly bool _isSqlite;

    public ApiCredentialConfiguration(bool isSqlite) => _isSqlite = isSqlite;

    public void Configure(EntityTypeBuilder<ApiCredential> builder)
    {
        builder.ToTable("ApiCredentials", IdentityDbContext.Schema);
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).HasConversion(id => id.Value, v => new ApiCredentialId(v)).ValueGeneratedNever();
        builder.ConfigureAggregate(_isSqlite);

        builder.Property(c => c.PartnerAccountId).HasConversion(id => id.Value, v => new AccountId(v));
        builder.Property(c => c.KeyId).HasMaxLength(64).IsRequired();
        builder.Property(c => c.SecretHash).HasMaxLength(256).IsRequired();
        builder.Property(c => c.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(c => c.IpWhitelist)
            .HasConversion(w => JsonSerializer.Serialize(w.Entries, (JsonSerializerOptions?)null),
                s => IpWhitelist.Create(JsonSerializer.Deserialize<string[]>(s, (JsonSerializerOptions?)null)))
            .HasColumnName("IpWhitelistJson").IsRequired();
        builder.Property(c => c.Limits)
            .HasConversion(l => JsonSerializer.Serialize(new LimitsJson(l.MaxRequests, l.PeriodSeconds), (JsonSerializerOptions?)null),
                s => ToLimits(s))
            .HasColumnName("LimitsJson").IsRequired();

        builder.HasIndex(c => c.KeyId).IsUnique().HasDatabaseName("UQ_ApiCredentials_KeyId");
        builder.HasIndex(c => c.PartnerAccountId).HasDatabaseName("IX_ApiCredentials_PartnerAccountId");

        // INV-13: at most one Active credential per partner.
        builder.HasIndex(c => c.PartnerAccountId).IsUnique().HasFilter("[Status] = 'Active'").HasDatabaseName("UQ_ApiCredentials_OneActivePerPartner");
    }

    private static UsageLimits ToLimits(string json)
    {
        var l = JsonSerializer.Deserialize<LimitsJson>(json, (JsonSerializerOptions?)null)!;
        return new UsageLimits(l.MaxRequests, l.PeriodSeconds);
    }

    private sealed record LimitsJson(int MaxRequests, int PeriodSeconds);
}

internal sealed class PasswordPolicyConfiguration : IEntityTypeConfiguration<PasswordPolicy>
{
    private readonly bool _isSqlite;

    public PasswordPolicyConfiguration(bool isSqlite) => _isSqlite = isSqlite;

    public void Configure(EntityTypeBuilder<PasswordPolicy> builder)
    {
        builder.ToTable("PasswordPolicy", IdentityDbContext.Schema);
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).ValueGeneratedNever();
        builder.ConfigureAggregate(_isSqlite);
    }
}

internal sealed class SessionTimeoutSettingConfiguration : IEntityTypeConfiguration<SessionTimeoutSetting>
{
    private readonly bool _isSqlite;

    public SessionTimeoutSettingConfiguration(bool isSqlite) => _isSqlite = isSqlite;

    public void Configure(EntityTypeBuilder<SessionTimeoutSetting> builder)
    {
        builder.ToTable("SessionSettings", IdentityDbContext.Schema);
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).ValueGeneratedNever();
        builder.ConfigureAggregate(_isSqlite);
    }
}

internal sealed class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    private readonly bool _isSqlite;

    public RoleConfiguration(bool isSqlite) => _isSqlite = isSqlite;

    public void Configure(EntityTypeBuilder<Role> builder)
    {
        builder.ToTable("Roles", IdentityDbContext.Schema);
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).HasConversion(id => id.Value, v => new RoleId(v)).ValueGeneratedNever();
        builder.ConfigureAggregate(_isSqlite);
        builder.Property(r => r.Name).HasMaxLength(100).IsRequired();
        builder.HasIndex(r => r.Name).IsUnique().HasDatabaseName("UQ_Roles_Name");

        builder.OwnsMany(r => r.Permissions, permissions =>
        {
            permissions.ToTable("RolePermissions", IdentityDbContext.Schema);
            permissions.WithOwner().HasForeignKey("RoleId");
            permissions.Property(p => p.Permission).HasMaxLength(100).IsRequired();
            permissions.HasKey("RoleId", nameof(RolePermission.Permission));
        });
        builder.Navigation(r => r.Permissions).HasField("_permissions").UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class PrivacyConsentConfiguration : IEntityTypeConfiguration<PrivacyConsent>
{
    private readonly bool _isSqlite;

    public PrivacyConsentConfiguration(bool isSqlite) => _isSqlite = isSqlite;

    public void Configure(EntityTypeBuilder<PrivacyConsent> builder)
    {
        builder.ToTable("PrivacyConsents", IdentityDbContext.Schema);
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedNever();
        builder.ConfigureAggregate(_isSqlite);
        builder.Property(c => c.PolicyVersion).HasMaxLength(50).IsRequired();
        builder.Property(c => c.Choices)
            .HasConversion(c => JsonSerializer.Serialize(new ChoicesJson(c.Analytics, c.Preferences, c.Marketing), (JsonSerializerOptions?)null),
                s => ToChoices(s))
            .HasColumnName("ChoicesJson").IsRequired();
        builder.Property(c => c.Locale).HasConversion<string>().HasMaxLength(5);

        // US-4.1-04 AC-05: no duplicate consent record per guest and policy version.
        builder.HasIndex(c => new { c.GuestId, c.PolicyVersion }).IsUnique().HasDatabaseName("UQ_PrivacyConsents_GuestId_PolicyVersion");
    }

    private static ConsentChoices ToChoices(string json)
    {
        var c = JsonSerializer.Deserialize<ChoicesJson>(json, (JsonSerializerOptions?)null)!;
        return new ConsentChoices(c.Analytics, c.Preferences, c.Marketing);
    }

    private sealed record ChoicesJson(bool Analytics, bool Preferences, bool Marketing);
}

internal sealed class AccessLogRecordConfiguration : IEntityTypeConfiguration<AccessLogRecord>
{
    public void Configure(EntityTypeBuilder<AccessLogRecord> builder)
    {
        builder.ToTable("AccessLog", IdentityDbContext.Schema);
        builder.HasKey(l => l.Id);
        builder.Property(l => l.Id).ValueGeneratedNever();
        builder.Property(l => l.Action).HasMaxLength(200).IsRequired();
        builder.Property(l => l.Resource).HasMaxLength(200).IsRequired();
        builder.Property(l => l.Decision).HasMaxLength(20).IsRequired();
        builder.Property(l => l.Reason).HasMaxLength(200);
        builder.Property(l => l.IpAddress).HasMaxLength(64);
        builder.HasIndex(l => l.AtUtc).HasDatabaseName("IX_AccessLog_At");
        builder.HasIndex(l => new { l.AccountId, l.AtUtc }).HasDatabaseName("IX_AccessLog_AccountId_At");
    }
}

internal sealed class SigningKeyRecordConfiguration : IEntityTypeConfiguration<SigningKeyRecord>
{
    public void Configure(EntityTypeBuilder<SigningKeyRecord> builder)
    {
        builder.ToTable("SigningKeys", IdentityDbContext.Schema);
        builder.HasKey(k => k.Kid);
        builder.Property(k => k.Kid).HasMaxLength(64);
        builder.Property(k => k.PublicKey).IsRequired();
        builder.Property(k => k.EncryptedPrivateKey).IsRequired();
    }
}
