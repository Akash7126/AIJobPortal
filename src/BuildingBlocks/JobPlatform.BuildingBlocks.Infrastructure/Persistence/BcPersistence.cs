using JobPlatform.SharedKernel.Application.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace JobPlatform.BuildingBlocks.Infrastructure.Persistence;

/// <summary>Database settings shared by every BC service (section "Database").</summary>
public sealed class ServiceDatabaseOptions
{
    public const string SectionName = "Database";

    /// <summary>"SqlServer" or "Sqlite" (local development and tests).</summary>
    public string Provider { get; set; } = "SqlServer";

    /// <summary>Apply EF migrations at startup (SQL Server). Production uses a migration job instead (foundation section 8).</summary>
    public bool ApplyMigrationsOnStartup { get; set; }
}

/// <summary>Holds the connection that keeps a shared in-memory SQLite database alive (null when not applicable).</summary>
public sealed class BcSqliteKeepAlive : IDisposable
{
    private readonly SqliteConnection? _connection;

    public BcSqliteKeepAlive(SqliteConnection? connection) => _connection = connection;

    public void Dispose() => _connection?.Dispose();
}

/// <summary>The model differs by provider (rowversion vs stamped token), so the model cache must be keyed by provider as well.</summary>
public sealed class ProviderAwareModelCacheKeyFactory : IModelCacheKeyFactory
{
    public object Create(DbContext context, bool designTime) => (context.GetType(), context.Database.ProviderName, designTime);
}

/// <summary>Seeds reference data after the schema exists. Must be idempotent.</summary>
public interface IDbSeeder<in TContext> where TContext : DbContext
{
    Task SeedAsync(TContext db, CancellationToken ct);
}

public static class BcPersistenceExtensions
{
    /// <summary>
    /// Registers a BC DbContext (Database:Provider = SqlServer|Sqlite, ConnectionStrings:&lt;connectionName&gt;), the outbox interceptor,
    /// the unit of work, the durable idempotency store and the startup initializer (EnsureCreated on SQLite, Migrate when enabled on SQL Server).
    /// </summary>
    public static IServiceCollection AddBcDbContext<TContext>(this IServiceCollection services, IConfiguration configuration, string connectionName,
        string schema) where TContext : BaseDbContext
    {
        services.Configure<ServiceDatabaseOptions>(configuration.GetSection(ServiceDatabaseOptions.SectionName));
        services.AddSingleton(sp =>
        {
            var cs = sp.GetRequiredService<IConfiguration>().GetConnectionString(connectionName) ?? string.Empty;
            if (!IsSqlite(sp) || !cs.Contains("mode=memory", StringComparison.OrdinalIgnoreCase))
            {
                return new BcSqliteKeepAlive(null);
            }

            // A shared in-memory database lives only while one connection stays open.
            var keepAlive = new SqliteConnection(cs);
            keepAlive.Open();
            return new BcSqliteKeepAlive(keepAlive);
        });

        services.AddDbContext<TContext>((sp, options) =>
        {
            var connectionString = sp.GetRequiredService<IConfiguration>().GetConnectionString(connectionName)
                                   ?? throw new InvalidOperationException($"ConnectionStrings:{connectionName} is not configured.");
            if (IsSqlite(sp))
            {
                sp.GetRequiredService<BcSqliteKeepAlive>();
                options.UseSqlite(connectionString, sqlite => sqlite.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery));
                options.ConfigureWarnings(w => w.Ignore(new EventId(30000)) /* SQLite ignores schemas: expected in dev/tests */);
            }
            else
            {
                options.UseSqlServer(connectionString, sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", schema)
                    .UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery));
            }

            options.AddInterceptors(sp.GetRequiredService<OutboxSaveChangesInterceptor>());
            options.ReplaceService<IModelCacheKeyFactory, ProviderAwareModelCacheKeyFactory>();
        });
        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<TContext>());
        services.AddSingleton<DbContextInitializer<TContext>>();
        services.AddHostedService(sp => sp.GetRequiredService<DbContextInitializer<TContext>>());
        return services;
    }

    public static IServiceCollection AddDbSeeder<TContext, TSeeder>(this IServiceCollection services)
        where TContext : DbContext where TSeeder : class, IDbSeeder<TContext>
    {
        services.AddScoped<IDbSeeder<TContext>, TSeeder>();
        return services;
    }

    private static bool IsSqlite(IServiceProvider sp) =>
        string.Equals(sp.GetRequiredService<IConfiguration>()[$"{ServiceDatabaseOptions.SectionName}:Provider"], "Sqlite", StringComparison.OrdinalIgnoreCase);
}

/// <summary>Startup hosted service: prepares the schema, then runs the registered seeders. Idempotent.</summary>
public sealed class DbContextInitializer<TContext> : IHostedService where TContext : DbContext
{
    private readonly IServiceProvider _services;
    private readonly ServiceDatabaseOptions _options;
    private readonly ILogger<DbContextInitializer<TContext>> _logger;

    public DbContextInitializer(IServiceProvider services, IOptions<ServiceDatabaseOptions> options, ILogger<DbContextInitializer<TContext>> logger)
    {
        _services = services;
        _options = options.Value;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = _services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TContext>();
        if (string.Equals(_options.Provider, "Sqlite", StringComparison.OrdinalIgnoreCase))
        {
            await db.Database.EnsureCreatedAsync(cancellationToken);
        }
        else if (_options.ApplyMigrationsOnStartup)
        {
            _logger.LogInformation("Applying migrations for {Context}", typeof(TContext).Name);
            await db.Database.MigrateAsync(cancellationToken);
        }

        foreach (var seeder in scope.ServiceProvider.GetServices<IDbSeeder<TContext>>())
        {
            await seeder.SeedAsync(db, cancellationToken);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
