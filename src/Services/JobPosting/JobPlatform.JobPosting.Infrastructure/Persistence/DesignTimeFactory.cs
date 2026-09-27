using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace JobPlatform.JobPosting.Infrastructure.Persistence;

/// <summary>Used only by "dotnet ef" to scaffold migrations against SQL Server; no connection is opened.</summary>
internal sealed class JobPostingDbContextDesignTimeFactory : IDesignTimeDbContextFactory<JobPostingDbContext>
{
    public JobPostingDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<JobPostingDbContext>()
            .UseSqlServer("Server=(local);Database=JobPlatform_JobPosting;Trusted_Connection=True;TrustServerCertificate=True",
                sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", JobPostingDbContext.Schema))
            .Options;
        return new JobPostingDbContext(options);
    }
}
