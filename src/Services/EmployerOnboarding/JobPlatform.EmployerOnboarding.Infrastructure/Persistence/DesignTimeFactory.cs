using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace JobPlatform.EmployerOnboarding.Infrastructure.Persistence;

/// <summary>Used only by "dotnet ef" to scaffold migrations against SQL Server; no connection is opened.</summary>
internal sealed class EmployerOnboardingDbContextDesignTimeFactory : IDesignTimeDbContextFactory<EmployerOnboardingDbContext>
{
    public EmployerOnboardingDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<EmployerOnboardingDbContext>()
            .UseSqlServer("Server=(local);Database=JobPlatform_EmployerOnboarding;Trusted_Connection=True;TrustServerCertificate=True",
                sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", EmployerOnboardingDbContext.Schema))
            .Options;
        return new EmployerOnboardingDbContext(options);
    }
}
