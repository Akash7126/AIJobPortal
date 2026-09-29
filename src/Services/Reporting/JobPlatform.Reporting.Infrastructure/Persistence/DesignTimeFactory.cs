using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace JobPlatform.Reporting.Infrastructure.Persistence;

/// <summary>Used only by "dotnet ef" to scaffold migrations against SQL Server; no connection is opened.</summary>
internal sealed class ReportingDbContextDesignTimeFactory : IDesignTimeDbContextFactory<ReportingDbContext>
{
    public ReportingDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<ReportingDbContext>()
            .UseSqlServer("Server=IT-Akash;Database=JobPlatform_Reporting;Trusted_Connection=True;TrustServerCertificate=True",
                sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", ReportingDbContext.Schema))
            .Options;
        return new ReportingDbContext(options);
    }
}
