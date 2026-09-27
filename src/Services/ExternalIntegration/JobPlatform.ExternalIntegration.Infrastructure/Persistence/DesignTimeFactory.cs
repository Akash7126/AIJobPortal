using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace JobPlatform.ExternalIntegration.Infrastructure.Persistence;

/// <summary>Used only by "dotnet ef" to scaffold migrations against SQL Server; no connection is opened.</summary>
internal sealed class ExternalIntegrationDbContextDesignTimeFactory : IDesignTimeDbContextFactory<ExternalIntegrationDbContext>
{
    public ExternalIntegrationDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<ExternalIntegrationDbContext>()
            .UseSqlServer("Server=(local);Database=JobPlatform_ExternalIntegration;Trusted_Connection=True;TrustServerCertificate=True",
                sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", ExternalIntegrationDbContext.Schema))
            .Options;
        return new ExternalIntegrationDbContext(options);
    }
}
