using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace JobPlatform.CandidateSourcing.Infrastructure.Persistence;

/// <summary>Used only by "dotnet ef" to scaffold migrations against SQL Server; no connection is opened.</summary>
internal sealed class CandidateSourcingDbContextDesignTimeFactory : IDesignTimeDbContextFactory<CandidateSourcingDbContext>
{
    public CandidateSourcingDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<CandidateSourcingDbContext>()
            .UseSqlServer("Server=IT-Akash;Database=JobPlatform_CandidateSourcing;Trusted_Connection=True;TrustServerCertificate=True",
                sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", CandidateSourcingDbContext.Schema))
            .Options;
        return new CandidateSourcingDbContext(options);
    }
}
