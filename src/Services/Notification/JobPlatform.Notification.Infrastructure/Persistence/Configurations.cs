using System.Text.Json;
using JobPlatform.BuildingBlocks.Infrastructure.Persistence;
using JobPlatform.Notification.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace JobPlatform.Notification.Infrastructure.Persistence;

/// <summary>Mapping only (conversions, indexes, constraints). Business rules stay in the domain.</summary>
internal sealed class InAppNotificationConfiguration : IEntityTypeConfiguration<InAppNotification>
{
    private readonly bool _isSqlite;

    public InAppNotificationConfiguration(bool isSqlite) => _isSqlite = isSqlite;

    public void Configure(EntityTypeBuilder<InAppNotification> builder)
    {
        builder.ToTable("InAppNotifications", NotificationDbContext.Schema);
        builder.HasKey(n => n.Id);
        builder.Property(n => n.Id).ValueGeneratedNever();
        builder.ConfigureAggregate(_isSqlite);
        builder.Ignore(n => n.Title);
        builder.Ignore(n => n.Body);
        builder.Property(n => n.TypeCode).HasMaxLength(64).IsRequired();
        builder.Property(n => n.Category).HasMaxLength(32).IsRequired();
        builder.Property(n => n.TitleAr).HasMaxLength(300);
        builder.Property(n => n.TitleEn).HasMaxLength(300);
        builder.Property(n => n.BodyAr).HasMaxLength(2000);
        builder.Property(n => n.BodyEn).HasMaxLength(2000);
        builder.Property(n => n.ActionUrl).HasMaxLength(500);
        builder.Property(n => n.Status).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.HasIndex(n => new { n.RecipientAccountId, n.Status, n.CreatedAtUtc }).IsDescending(false, false, true).HasDatabaseName("IX_InApp_Recipient_Status_Created");
    }
}

internal sealed class OutboundMessageConfiguration : IEntityTypeConfiguration<OutboundMessage>
{
    private readonly bool _isSqlite;

    public OutboundMessageConfiguration(bool isSqlite) => _isSqlite = isSqlite;

    public void Configure(EntityTypeBuilder<OutboundMessage> builder)
    {
        builder.ToTable("OutboundMessages", NotificationDbContext.Schema);
        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).ValueGeneratedNever();
        builder.ConfigureAggregate(_isSqlite);
        builder.Property(m => m.Channel).HasConversion<string>().HasMaxLength(8).IsRequired();
        builder.Property(m => m.Category).HasMaxLength(32).IsRequired();
        builder.Property(m => m.DedupeKey).HasMaxLength(200).IsRequired();
        builder.Property(m => m.TemplateCode).HasMaxLength(64);
        builder.Property(m => m.Subject).HasMaxLength(300).IsRequired();
        builder.Property(m => m.Locale).HasMaxLength(8).IsRequired();
        builder.Property(m => m.Status).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(m => m.SuppressionReason).HasMaxLength(100);
        builder.Property(m => m.ErrorCode).HasMaxLength(64);
        builder.Property(m => m.DeliveryStatus).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(m => m.ProviderMessageId).HasMaxLength(200);
        builder.Property(m => m.SenderIdentity).HasMaxLength(200);

        // INV-06: the same event never yields a second message (final guard behind the composer's check).
        builder.HasIndex(m => m.DedupeKey).IsUnique().HasDatabaseName("UQ_OutboundMessages_DedupeKey");
        builder.HasIndex(m => new { m.Status, m.NextAttemptUtc }).HasDatabaseName("IX_OutboundMessages_Status_NextAttempt");
        builder.HasIndex(m => m.ProviderMessageId).HasDatabaseName("IX_OutboundMessages_ProviderMessageId");
    }
}

internal sealed class NotificationPreferenceConfiguration : IEntityTypeConfiguration<NotificationPreference>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public void Configure(EntityTypeBuilder<NotificationPreference> builder)
    {
        builder.ToTable("NotificationPreferences", NotificationDbContext.Schema);
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).HasColumnName("AccountId").ValueGeneratedNever();
        builder.Ignore(p => p.EmailCategories);
        builder.Ignore(p => p.InAppCategories);
        builder.Ignore(p => p.Unsubscribed);
        builder.Property(p => p.EmailMode).HasConversion<string>().HasMaxLength(12).IsRequired();
        builder.Property(p => p.Mobile).HasMaxLength(20);

        // The three collections are private fields; they are stored as JSON and compared by content so in-place changes are detected.
        MapJson<Dictionary<string, bool>>(builder, "_email", "EmailJson");
        MapJson<Dictionary<string, bool>>(builder, "_inApp", "InAppJson");
        MapJson<HashSet<string>>(builder, "_unsubscribed", "UnsubscribedJson");
    }

    private static void MapJson<T>(EntityTypeBuilder<NotificationPreference> builder, string field, string column) where T : class, new()
    {
        var converter = new ValueConverter<T, string>(v => JsonSerializer.Serialize(v, Json), s => JsonSerializer.Deserialize<T>(s, Json) ?? new T());
        var comparer = new ValueComparer<T>((a, b) => JsonSerializer.Serialize(a, Json) == JsonSerializer.Serialize(b, Json),
            v => JsonSerializer.Serialize(v, Json).GetHashCode(), v => JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(v, Json), Json)!);
        builder.Property<T>(field).HasColumnName(column).HasConversion(converter, comparer).IsRequired();
    }
}

internal sealed class EmailTemplateConfiguration : IEntityTypeConfiguration<EmailTemplate>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public void Configure(EntityTypeBuilder<EmailTemplate> builder)
    {
        builder.ToTable("EmailTemplates", NotificationDbContext.Schema);
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).ValueGeneratedNever();
        builder.Property(t => t.Code).HasMaxLength(64).IsRequired();
        builder.Property(t => t.Locale).HasMaxLength(8).IsRequired();
        builder.Property(t => t.Subject).HasMaxLength(200).IsRequired();
        builder.Property(t => t.Placeholders).HasConversion(
            v => JsonSerializer.Serialize(v, Json),
            s => (IReadOnlyDictionary<string, string>)(JsonSerializer.Deserialize<Dictionary<string, string>>(s, Json) ?? new Dictionary<string, string>()),
            new ValueComparer<IReadOnlyDictionary<string, string>>((a, b) => JsonSerializer.Serialize(a, Json) == JsonSerializer.Serialize(b, Json),
                v => JsonSerializer.Serialize(v, Json).GetHashCode(), v => new Dictionary<string, string>(v))).HasColumnName("PlaceholdersJson").IsRequired();
        builder.HasIndex(t => new { t.Code, t.Version, t.Locale }).IsUnique().HasDatabaseName("UQ_EmailTemplates_Code_Version_Locale");
    }
}

internal sealed class NotificationTypeConfiguration : IEntityTypeConfiguration<NotificationType>
{
    public void Configure(EntityTypeBuilder<NotificationType> builder)
    {
        builder.ToTable("NotificationTypes", NotificationDbContext.Schema);
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).HasColumnName("Code").HasMaxLength(64).ValueGeneratedNever();
        builder.Property(t => t.Icon).HasMaxLength(32).IsRequired();
        builder.Property(t => t.Colour).HasMaxLength(7).IsRequired();
        builder.Property(t => t.TextAlternative).HasMaxLength(200).IsRequired();
    }
}

internal sealed class SmsPolicyConfiguration : IEntityTypeConfiguration<SmsPolicy>
{
    public void Configure(EntityTypeBuilder<SmsPolicy> builder)
    {
        builder.ToTable("SmsPolicy", NotificationDbContext.Schema);
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).HasColumnName("Version").ValueGeneratedNever();
        builder.Property(p => p.EssentialCategories).HasConversion(
            v => string.Join(',', v),
            s => (IReadOnlyCollection<string>)s.Split(',', StringSplitOptions.RemoveEmptyEntries),
            new ValueComparer<IReadOnlyCollection<string>>((a, b) => string.Join(',', a ?? Array.Empty<string>()) == string.Join(',', b ?? Array.Empty<string>()), v => string.Join(',', v).GetHashCode(),
                v => v.ToArray()))
            .HasColumnName("EssentialCategories").HasMaxLength(500).IsRequired();
    }
}

internal sealed class JobConfirmationConfiguration : IEntityTypeConfiguration<JobConfirmation>
{
    private readonly bool _isSqlite;

    public JobConfirmationConfiguration(bool isSqlite) => _isSqlite = isSqlite;

    public void Configure(EntityTypeBuilder<JobConfirmation> builder)
    {
        builder.ToTable("JobConfirmations", NotificationDbContext.Schema);
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedNever();
        builder.ConfigureAggregate(_isSqlite);
        builder.Property(c => c.SourceJobId).HasMaxLength(128).IsRequired();
        builder.Property(c => c.PlatformJobId).HasMaxLength(128).IsRequired();
        builder.HasIndex(c => new { c.SourcePlatformId, c.SourceJobId }).IsUnique().HasDatabaseName("UQ_JobConfirmations_Source");
    }
}

internal sealed class WeeklyCycleConfiguration : IEntityTypeConfiguration<WeeklyCycle>
{
    public void Configure(EntityTypeBuilder<WeeklyCycle> builder)
    {
        builder.ToTable("WeeklyCycle", NotificationDbContext.Schema);
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedNever();
        builder.Property(c => c.IsoWeek).HasMaxLength(10).IsRequired();
        builder.HasIndex(c => new { c.AccountId, c.IsoWeek }).IsUnique().HasDatabaseName("UQ_WeeklyCycle_Account_Week");
    }
}
