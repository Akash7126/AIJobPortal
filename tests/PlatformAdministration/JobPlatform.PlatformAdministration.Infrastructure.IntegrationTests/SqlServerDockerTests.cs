using JobPlatform.PlatformAdministration.Domain.Entities;
using JobPlatform.PlatformAdministration.Domain.Taxonomy;
using JobPlatform.PlatformAdministration.Infrastructure.Persistence;
using JobPlatform.BuildingBlocks.Infrastructure.Persistence;
using JobPlatform.SharedKernel.Application.Persistence;
using JobPlatform.TestSupport;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Testcontainers.MsSql;

namespace JobPlatform.PlatformAdministration.Infrastructure.IntegrationTests;

/// <summary>
/// Tests against real SQL Server (Testcontainers). Written and compiled here, but skipped automatically when Docker is not installed:
/// they have NOT been executed on the machine that produced this code base.
/// </summary>
public class SqlServerTests
{
    [DockerFact]
    [Trait("Category", "Docker")]
    public async Task Migration_Applies_AndTheUniqueIndexesAndRowVersionHoldOnRealSqlServer()
    {
        await using var container = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();
        await container.StartAsync();
        var options = new DbContextOptionsBuilder<AdminDbContext>()
            .UseSqlServer(container.GetConnectionString(), sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", AdminDbContext.Schema))
            .ReplaceService<IModelCacheKeyFactory, ProviderAwareModelCacheKeyFactory>()
            .Options;

        await using var db = new AdminDbContext(options);
        await db.Database.MigrateAsync();

        var core = new Dictionary<string, string> { ["companyName"] = "Acme", ["companyId"] = "CO-1" };
        db.PlatformEntityRecords.Add(PlatformEntityRecord.Create(Guid.NewGuid(), PlatformEntityType.Employer, core, Db.Admin, false, Db.T0));
        await db.SaveChangesAsync();
        db.PlatformEntityRecords.Add(PlatformEntityRecord.Create(Guid.NewGuid(), PlatformEntityType.Employer, core, Db.Admin, false, Db.T0));
        var duplicate = () => db.SaveChangesAsync();
        await duplicate.Should().ThrowAsync<UniqueConstraintViolationException>("UQ_PlatformEntityRecords_Type_Key holds on SQL Server");
        db.ChangeTracker.Clear();

        var taxonomy = PlatformTaxonomy.Create("skills", Db.T0);
        db.Taxonomies.Add(taxonomy);
        await db.SaveChangesAsync();
        await using var other = new AdminDbContext(options);
        var first = await other.Taxonomies.Include(t => t.Nodes).SingleAsync();
        var second = await db.Taxonomies.Include(t => t.Nodes).SingleAsync();
        first.ApplyChanges(new[] { Db.Add("a") }, Db.Admin, Db.T0);
        await other.SaveChangesAsync();
        second.ApplyChanges(new[] { Db.Add("b") }, Db.Admin, Db.T0);
        var stale = () => db.SaveChangesAsync();
        await stale.Should().ThrowAsync<ConcurrencyConflictException>("rowversion detects the lost race that LaterSaveWinsBehavior retries");
    }
}
