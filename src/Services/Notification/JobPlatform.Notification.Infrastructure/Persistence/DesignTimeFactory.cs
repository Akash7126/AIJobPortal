using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace JobPlatform.Notification.Infrastructure.Persistence;

/// <summary>Used only by "dotnet ef" to scaffold migrations against SQL Server; no connection is opened.</summary>
internal sealed class NotificationDbContextDesignTimeFactory : IDesignTimeDbContextFactory<NotificationDbContext>
{
    public NotificationDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<NotificationDbContext>()
            .UseSqlServer("Server=(local);Database=JobPlatform_Notification;Trusted_Connection=True;TrustServerCertificate=True",
                sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", NotificationDbContext.Schema))
            .Options;
        return new NotificationDbContext(options);
    }
}
