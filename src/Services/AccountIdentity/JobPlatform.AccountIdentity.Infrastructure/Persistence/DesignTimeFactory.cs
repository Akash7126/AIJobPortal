using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace JobPlatform.AccountIdentity.Infrastructure.Persistence;

/// <summary>Used only by "dotnet ef" to scaffold migrations against SQL Server; no connection is opened.</summary>
internal sealed class IdentityDbContextDesignTimeFactory : IDesignTimeDbContextFactory<IdentityDbContext>
{
    public IdentityDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseSqlServer("Server=(local);Database=JobPlatform_AccountIdentity;Trusted_Connection=True;TrustServerCertificate=True",
                sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", IdentityDbContext.Schema))
            .Options;
        return new IdentityDbContext(options);
    }
}
