using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace JobPlatform.GovernmentIntegration.Infrastructure.Persistence;

/// <summary>Used only by "dotnet ef" to scaffold migrations against SQL Server; no connection is opened.</summary>
internal sealed class GovernmentIntegrationDbContextDesignTimeFactory : IDesignTimeDbContextFactory<GovernmentIntegrationDbContext>
{
    public GovernmentIntegrationDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<GovernmentIntegrationDbContext>()
            .UseSqlServer("Server=IT-Akash;Database=JobPlatform_GovernmentIntegration;Trusted_Connection=True;TrustServerCertificate=True",
                sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", GovernmentIntegrationDbContext.Schema))
            .Options;
        return new GovernmentIntegrationDbContext(options);
    }
}
