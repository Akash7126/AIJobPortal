using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace JobPlatform.JobSeekerProfile.Infrastructure.Persistence;

/// <summary>Used only by "dotnet ef" to scaffold migrations against SQL Server; no connection is opened.</summary>
internal sealed class JobSeekerProfileDbContextDesignTimeFactory : IDesignTimeDbContextFactory<JobSeekerProfileDbContext>
{
    public JobSeekerProfileDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<JobSeekerProfileDbContext>()
            .UseSqlServer("Server=IT-Akash;Database=JobPlatform_JobSeekerProfile;Trusted_Connection=True;TrustServerCertificate=True",
                sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", JobSeekerProfileDbContext.Schema))
            .Options;
        return new JobSeekerProfileDbContext(options);
    }
}
