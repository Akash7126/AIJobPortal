using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace JobPlatform.AiMatching.Infrastructure.Persistence;

/// <summary>Used only by "dotnet ef" to scaffold migrations against SQL Server; no connection is opened.</summary>
internal sealed class AiMatchingDbContextDesignTimeFactory : IDesignTimeDbContextFactory<AiMatchingDbContext>
{
    public AiMatchingDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<AiMatchingDbContext>()
            .UseSqlServer("Server=(local);Database=JobPlatform_AiMatching;Trusted_Connection=True;TrustServerCertificate=True",
                sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", AiMatchingDbContext.Schema))
            .Options;
        return new AiMatchingDbContext(options);
    }
}
