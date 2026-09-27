using System.Text.Json;
using JobPlatform.BuildingBlocks.Infrastructure.Persistence;
using JobPlatform.GovernmentIntegration.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace JobPlatform.GovernmentIntegration.Infrastructure.Persistence;

internal static class Json
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);
}

internal sealed class EmployerVerificationConfiguration(bool isSqlite) : IEntityTypeConfiguration<EmployerVerification>
{
    private sealed record SubmissionDto(string RegistrationNumber, string VatNumber, string MobileNumber, Dictionary<string, string> AdditionalFields);

    public void Configure(EntityTypeBuilder<EmployerVerification> builder)
    {
        builder.ToTable("EmployerVerifications", GovernmentIntegrationDbContext.Schema);
        builder.HasKey(v => v.Id);
        builder.Property(v => v.Id).ValueGeneratedNever();
        builder.ConfigureAggregate(isSqlite);
        builder.Property(v => v.State).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(v => v.Method).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(v => v.Submission).HasColumnName("SubmissionJson")
            .HasConversion(new ValueConverter<Submission, string>(
                v => JsonSerializer.Serialize(new SubmissionDto(v.RegistrationNumber, v.VatNumber, v.MobileNumber, v.AdditionalFields.ToDictionary(p => p.Key, p => p.Value)), Json.Options),
                s => ToSubmission(JsonSerializer.Deserialize<SubmissionDto>(s, Json.Options)!)))
            .IsRequired();
        builder.HasIndex(v => new { v.EmployerAccountId, v.State }).HasDatabaseName("IX_EmployerVerifications_EmployerAccountId_State");
        builder.HasMany(v => v.Attempts).WithOne().HasForeignKey("EmployerVerificationId").OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(v => v.Attempts).UsePropertyAccessMode(PropertyAccessMode.Field);
    }

    private static Submission ToSubmission(SubmissionDto d) => new(d.RegistrationNumber, d.VatNumber, d.MobileNumber, d.AdditionalFields);
}

internal sealed class VerificationAttemptConfiguration : IEntityTypeConfiguration<VerificationAttempt>
{
    public void Configure(EntityTypeBuilder<VerificationAttempt> builder)
    {
        builder.ToTable("EmployerVerificationAttempts", GovernmentIntegrationDbContext.Schema);
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).ValueGeneratedNever();
        builder.Property(a => a.Source).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(a => a.Outcome).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(a => a.ErrorCode).HasMaxLength(100);
        builder.HasIndex("EmployerVerificationId", nameof(VerificationAttempt.AttemptNo)).IsUnique()
            .HasDatabaseName("UQ_EmployerVerificationAttempts_Verification_AttemptNo");
    }
}

internal sealed class GovernmentVerificationDataConfiguration(bool isSqlite) : IEntityTypeConfiguration<GovernmentVerificationData>
{
    public void Configure(EntityTypeBuilder<GovernmentVerificationData> builder)
    {
        builder.ToTable("GovernmentVerificationData", GovernmentIntegrationDbContext.Schema);
        builder.HasKey(d => d.Id);
        builder.Property(d => d.Id).ValueGeneratedNever();
        builder.ConfigureAggregate(isSqlite);
        builder.OwnsOne(d => d.Subject, s =>
        {
            s.Property(x => x.SubjectType).HasColumnName("SubjectType").HasConversion<string>().HasMaxLength(32).IsRequired();
            s.Property(x => x.SubjectId).HasColumnName("SubjectId").IsRequired();
            s.HasIndex(x => new { x.SubjectType, x.SubjectId }).HasDatabaseName("IX_GovernmentVerificationData_Subject");
        });
        builder.Navigation(d => d.Subject).IsRequired();
        builder.Property(d => d.Source).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(d => d.Status).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(d => d.Purpose).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(d => d.VerifiedFields).HasColumnName("VerifiedFieldsJson")
            .HasConversion(new ValueConverter<IReadOnlyCollection<VerifiedField>, string>(
                v => JsonSerializer.Serialize(v.Select(f => new[] { f.Name, f.Value }), Json.Options),
                s => JsonSerializer.Deserialize<List<string[]>>(s, Json.Options)!.Select(p => new VerifiedField(p[0], p[1])).ToList()))
            .IsRequired();
        builder.HasIndex(d => d.RetentionExpiresAtUtc).HasDatabaseName("IX_GovernmentVerificationData_RetentionExpiresAtUtc");
    }
}

internal sealed class EducationalCredentialVerificationConfiguration(bool isSqlite) : IEntityTypeConfiguration<EducationalCredentialVerification>
{
    public void Configure(EntityTypeBuilder<EducationalCredentialVerification> builder)
    {
        builder.ToTable("EducationalCredentialVerifications", GovernmentIntegrationDbContext.Schema);
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedNever();
        builder.ConfigureAggregate(isSqlite);
        builder.OwnsOne(e => e.Credential, c =>
        {
            c.Property(x => x.Institution).HasColumnName("Institution").HasMaxLength(200).IsRequired();
            c.Property(x => x.CredentialName).HasColumnName("CredentialName").HasMaxLength(200).IsRequired();
            c.Property(x => x.Year).HasColumnName("Year").IsRequired();
        });
        builder.Navigation(e => e.Credential).IsRequired();
        builder.Property(e => e.Status).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.HasIndex(e => e.SubjectId).HasDatabaseName("IX_EducationalCredentialVerifications_SubjectId");
    }
}

internal sealed class IdentityVerificationDataConfiguration(bool isSqlite) : IEntityTypeConfiguration<IdentityVerificationData>
{
    public void Configure(EntityTypeBuilder<IdentityVerificationData> builder)
    {
        builder.ToTable("IdentityVerifications", GovernmentIntegrationDbContext.Schema);
        builder.HasKey(i => i.Id);
        builder.Property(i => i.Id).ValueGeneratedNever();
        builder.ConfigureAggregate(isSqlite);
        builder.OwnsOne(i => i.Subject, s =>
        {
            s.Property(x => x.SubjectType).HasColumnName("SubjectType").HasConversion<string>().HasMaxLength(32).IsRequired();
            s.Property(x => x.SubjectId).HasColumnName("SubjectId").IsRequired();
            s.HasIndex(x => x.SubjectId).HasDatabaseName("IX_IdentityVerifications_SubjectId");
        });
        builder.Navigation(i => i.Subject).IsRequired();
        builder.OwnsOne(i => i.IdentityClaim, c =>
        {
            c.Property(x => x.NationalIdReference).HasColumnName("NationalIdReference").HasMaxLength(64).IsRequired();
            c.Property(x => x.FullName).HasColumnName("FullName").HasMaxLength(200).IsRequired();
            c.Property(x => x.DateOfBirth).HasColumnName("DateOfBirth").IsRequired();
        });
        builder.Navigation(i => i.IdentityClaim).IsRequired();
        builder.Property(i => i.Status).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(i => i.UnverifiedReason).HasMaxLength(32);
    }
}

internal sealed class LegacyDataConfiguration(bool isSqlite) : IEntityTypeConfiguration<LegacyData>
{
    public void Configure(EntityTypeBuilder<LegacyData> builder)
    {
        builder.ToTable("LegacyData", GovernmentIntegrationDbContext.Schema);
        builder.HasKey(l => l.Id);
        builder.Property(l => l.Id).ValueGeneratedNever();
        builder.ConfigureAggregate(isSqlite);
        builder.Property(l => l.SourceSystem).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(l => l.SourceRecordId).HasMaxLength(200).IsRequired();
        builder.Property(l => l.RecordType).HasMaxLength(100).IsRequired();
        builder.Property(l => l.Stage).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(l => l.ValidationErrors).HasColumnName("ValidationErrorsJson")
            .HasConversion(new ValueConverter<IReadOnlyCollection<string>, string>(
                v => JsonSerializer.Serialize(v, Json.Options),
                s => JsonSerializer.Deserialize<List<string>>(s, Json.Options)!))
            .IsRequired();
        builder.HasIndex(l => new { l.SourceSystem, l.SourceRecordId }).IsUnique().HasDatabaseName("UQ_LegacyData_SourceSystem_SourceRecordId");
        builder.HasIndex(l => new { l.MigrationBatchId, l.Stage }).HasDatabaseName("IX_LegacyData_MigrationBatchId_Stage");
    }
}

internal sealed class DataQualityConfiguration(bool isSqlite) : IEntityTypeConfiguration<DataQuality>
{
    public void Configure(EntityTypeBuilder<DataQuality> builder)
    {
        builder.ToTable("DataQuality", GovernmentIntegrationDbContext.Schema);
        builder.HasKey(d => d.Id);
        builder.Property(d => d.Id).ValueGeneratedNever();
        builder.ConfigureAggregate(isSqlite);
        builder.Property(d => d.SnapshotVersion).HasMaxLength(100).IsRequired();
        builder.Property(d => d.Status).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.HasIndex(d => d.BatchId).HasDatabaseName("IX_DataQuality_BatchId");
    }
}

internal sealed class MigrationRunConfiguration(bool isSqlite) : IEntityTypeConfiguration<MigrationRun>
{
    public void Configure(EntityTypeBuilder<MigrationRun> builder)
    {
        builder.ToTable("MigrationRuns", GovernmentIntegrationDbContext.Schema);
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).ValueGeneratedNever();
        builder.ConfigureAggregate(isSqlite);
        builder.Property(r => r.Status).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.HasIndex(r => r.Status).HasDatabaseName("IX_MigrationRuns_Status");
        builder.HasMany(r => r.Phases).WithOne().HasForeignKey("MigrationRunId").OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(r => r.Phases).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.HasMany(r => r.Log).WithOne().HasForeignKey("MigrationRunId").OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(r => r.Log).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class MigrationPhaseConfiguration : IEntityTypeConfiguration<MigrationPhase>
{
    public void Configure(EntityTypeBuilder<MigrationPhase> builder)
    {
        builder.ToTable("MigrationPhases", GovernmentIntegrationDbContext.Schema);
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).ValueGeneratedNever();
        builder.Property(p => p.Name).HasMaxLength(100).IsRequired();
        builder.Property(p => p.Status).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(p => p.TestOutcome).HasMaxLength(500);
    }
}

internal sealed class MigrationLogEntryConfiguration : IEntityTypeConfiguration<MigrationLogEntry>
{
    public void Configure(EntityTypeBuilder<MigrationLogEntry> builder)
    {
        builder.ToTable("MigrationLog", GovernmentIntegrationDbContext.Schema);
        builder.HasKey(l => l.Id);
        builder.Property(l => l.Id).ValueGeneratedNever();
        builder.Property(l => l.Phase).HasMaxLength(100).IsRequired();
        builder.Property(l => l.Outcome).HasMaxLength(32).IsRequired();
        builder.Property(l => l.Message).HasMaxLength(1000).IsRequired();
    }
}

internal sealed class GovernmentSourceConnectionConfiguration(bool isSqlite) : IEntityTypeConfiguration<GovernmentSourceConnection>
{
    public void Configure(EntityTypeBuilder<GovernmentSourceConnection> builder)
    {
        builder.ToTable("GovernmentSourceConnections", GovernmentIntegrationDbContext.Schema);
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedNever();
        builder.ConfigureAggregate(isSqlite);
        builder.Property(c => c.Source).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(c => c.Endpoint).HasMaxLength(500).IsRequired();
        builder.Property(c => c.AuthMethod).HasMaxLength(100).IsRequired();
        builder.Property(c => c.CredentialRef).HasMaxLength(200).IsRequired();
        builder.Property(c => c.Health).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.OwnsOne(c => c.LastKnownGood, l =>
        {
            l.Property(x => x.SnapshotRef).HasColumnName("LastKnownGoodSnapshotRef").HasMaxLength(200);
            l.Property(x => x.TakenAtUtc).HasColumnName("LastKnownGoodTakenAtUtc");
        });
        builder.Navigation(c => c.LastKnownGood).IsRequired(false);
        builder.HasIndex(c => c.Source).IsUnique().HasDatabaseName("UQ_GovernmentSourceConnections_Source");
    }
}

internal sealed class KnownAccountConfiguration : IEntityTypeConfiguration<KnownAccount>
{
    public void Configure(EntityTypeBuilder<KnownAccount> builder)
    {
        builder.ToTable("KnownAccounts", GovernmentIntegrationDbContext.Schema);
        builder.HasKey(a => a.AccountId);
        builder.Property(a => a.AccountId).ValueGeneratedNever();
        builder.Property(a => a.ActorType).HasConversion<string>().HasMaxLength(32).IsRequired();
    }
}

internal sealed class GovernmentDataAccessLogEntryConfiguration : IEntityTypeConfiguration<GovernmentDataAccessLogEntry>
{
    public void Configure(EntityTypeBuilder<GovernmentDataAccessLogEntry> builder)
    {
        builder.ToTable("GovernmentDataAccessLog", GovernmentIntegrationDbContext.Schema);
        builder.HasKey(l => l.Id);
        builder.Property(l => l.Id).ValueGeneratedNever();
        builder.Property(l => l.Component).HasMaxLength(100).IsRequired();
        builder.Property(l => l.Purpose).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(l => l.SubjectRef).HasMaxLength(200).IsRequired();
        builder.Property(l => l.Decision).HasMaxLength(16).IsRequired();
        builder.Property(l => l.ErrorCode).HasMaxLength(100);
        builder.HasIndex(l => l.OccurredAtUtc).HasDatabaseName("IX_GovernmentDataAccessLog_OccurredAtUtc");
    }
}
