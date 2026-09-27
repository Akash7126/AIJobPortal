using JobPlatform.BuildingBlocks.Infrastructure.Persistence;
using JobPlatform.Notification.Domain;
using Microsoft.EntityFrameworkCore;

namespace JobPlatform.Notification.Infrastructure.Persistence;

public sealed class NotificationDbContext : BaseDbContext
{
    public const string Schema = "notify";

    public NotificationDbContext(DbContextOptions<NotificationDbContext> options) : base(options)
    {
    }

    public DbSet<InAppNotification> InAppNotifications => Set<InAppNotification>();
    public DbSet<OutboundMessage> OutboundMessages => Set<OutboundMessage>();
    public DbSet<NotificationPreference> NotificationPreferences => Set<NotificationPreference>();
    public DbSet<EmailTemplate> EmailTemplates => Set<EmailTemplate>();
    public DbSet<NotificationType> NotificationTypes => Set<NotificationType>();
    public DbSet<SmsPolicy> SmsPolicies => Set<SmsPolicy>();
    public DbSet<JobConfirmation> JobConfirmations => Set<JobConfirmation>();
    public DbSet<WeeklyCycle> WeeklyCycles => Set<WeeklyCycle>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfiguration(new InAppNotificationConfiguration(IsSqlite));
        modelBuilder.ApplyConfiguration(new OutboundMessageConfiguration(IsSqlite));
        modelBuilder.ApplyConfiguration(new NotificationPreferenceConfiguration());
        modelBuilder.ApplyConfiguration(new EmailTemplateConfiguration());
        modelBuilder.ApplyConfiguration(new NotificationTypeConfiguration());
        modelBuilder.ApplyConfiguration(new SmsPolicyConfiguration());
        modelBuilder.ApplyConfiguration(new JobConfirmationConfiguration(IsSqlite));
        modelBuilder.ApplyConfiguration(new WeeklyCycleConfiguration());
        base.OnModelCreating(modelBuilder);
    }
}
