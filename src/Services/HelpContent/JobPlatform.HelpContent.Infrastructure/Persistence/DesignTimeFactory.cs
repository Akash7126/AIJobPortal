using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace JobPlatform.HelpContent.Infrastructure.Persistence;

/// <summary>Used only by "dotnet ef" to scaffold migrations against SQL Server; no connection is opened.</summary>
internal sealed class HelpContentDbContextDesignTimeFactory : IDesignTimeDbContextFactory<HelpContentDbContext>
{
    public HelpContentDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<HelpContentDbContext>()
            .UseSqlServer("Server=IT-Akash;Database=JobPlatform_HelpContent;Trusted_Connection=True;TrustServerCertificate=True",
                sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", HelpContentDbContext.Schema))
            .Options;
        return new HelpContentDbContext(options);
    }
}
