using JobPlatform.AuditLogging.Domain;
using JobPlatform.AuditLogging.Infrastructure.Persistence;
using JobPlatform.BuildingBlocks.Infrastructure.Persistence;
using JobPlatform.SharedKernel.Application.Persistence;
using JobPlatform.TestSupport;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Testcontainers.MsSql;

namespace JobPlatform.AuditLogging.Infrastructure.IntegrationTests;

/// <summary>
/// Tests against real SQL Server (Testcontainers). Written and compiled here, but skipped automatically when Docker is not installed:
/// they have NOT been executed on the machine that produced this code base.
/// </summary>
public class SqlServerTests
{
    [DockerFact]
    [Trait("Category", "Docker")]
    public async Task Migration_Applies_AndTheUniqueIndexesHoldOnRealSqlServer()
    {
        await using var container = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();
        await container.StartAsync();
        var options = new DbContextOptionsBuilder<AuditDbContext>()
            .UseSqlServer(container.GetConnectionString(), sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", AuditDbContext.Schema))
            .ReplaceService<IModelCacheKeyFactory, ProviderAwareModelCacheKeyFactory>()
            .Options;

        await using var db = new AuditDbContext(options);
        await db.Database.MigrateAsync();

        var message = Guid.NewGuid();
        db.AuditEntries.Add(Db.Entry(AuditCategory.Submission, Guid.NewGuid(), messageId: message));
        await db.SaveChangesAsync();
        db.AuditEntries.Add(Db.Entry(AuditCategory.Submission, Guid.NewGuid(), messageId: message));
        var act = () => db.SaveChangesAsync();
        await act.Should().ThrowAsync<UniqueConstraintViolationException>();

        var admin = Guid.NewGuid();
        db.ExportJobs.Add(ExportJob.Request(admin, ReportType.TopSearches, ExportFormat.Csv, new Dictionary<string, string>(), Db.T0));
        await db.SaveChangesAsync();
        db.ExportJobs.Add(ExportJob.Request(admin, ReportType.TopSearches, ExportFormat.Csv, new Dictionary<string, string>(), Db.T0));
        var duplicate = () => db.SaveChangesAsync();
        await duplicate.Should().ThrowAsync<UniqueConstraintViolationException>("the filtered unique index UQ_ExportJobs_InProgress holds on SQL Server");
    }
}
