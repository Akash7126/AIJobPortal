using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace JobPlatform.PlatformAdministration.Infrastructure.Persistence;

/// <summary>Used only by "dotnet ef" to scaffold migrations against SQL Server; no connection is opened.</summary>
internal sealed class AdminDbContextDesignTimeFactory : IDesignTimeDbContextFactory<AdminDbContext>
{
    public AdminDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<AdminDbContext>()
            .UseSqlServer("Server=IT-Akash;Database=JobPlatform_PlatformAdministration;Trusted_Connection=True;TrustServerCertificate=True",
                sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", AdminDbContext.Schema))
            .Options;
        return new AdminDbContext(options);
    }
}
