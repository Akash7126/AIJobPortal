using JobPlatform.AccountIdentity.Application.Interfaces;
using JobPlatform.AccountIdentity.Domain.Interfaces.Repositories;
using JobPlatform.AccountIdentity.Domain.Interfaces.Services;
using JobPlatform.AccountIdentity.Infrastructure.Caching;
using JobPlatform.AccountIdentity.Infrastructure.Delivery;
using JobPlatform.AccountIdentity.Infrastructure.Persistence;
using JobPlatform.AccountIdentity.Infrastructure.Persistence.Repositories;
using JobPlatform.AccountIdentity.Infrastructure.Security;
using JobPlatform.BuildingBlocks.Infrastructure.DependencyInjection;
using JobPlatform.BuildingBlocks.Infrastructure.Persistence;
using JobPlatform.SharedKernel.Application.Interfaces.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace JobPlatform.AccountIdentity.Infrastructure;

public static class DependencyInjection
{
    /// <summary>
    /// Wires persistence (Database:Provider = SqlServer|Sqlite), repositories, security services, Redis-or-in-memory stores,
    /// delivery adapters, the startup initializer and the outbox processor.
    /// </summary>
    public static IServiceCollection AddAccountIdentityInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<DatabaseOptions>(configuration.GetSection(DatabaseOptions.SectionName));
        services.Configure<SecurityOptions>(configuration.GetSection(SecurityOptions.SectionName));
        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));
        services.Configure<BootstrapOptions>(configuration.GetSection(BootstrapOptions.SectionName));
        services.Configure<ServiceClientOptions>(configuration.GetSection(ServiceClientOptions.SectionName));
        services.Configure<DeliveryOptions>(configuration.GetSection(DeliveryOptions.SectionName));

        // Database provider and connection string are read lazily so host-level overrides (tests, containers) always win.
        services.AddSingleton(sp =>
        {
            var cs = sp.GetRequiredService<IConfiguration>().GetConnectionString("Identity") ?? string.Empty;
            if (!IsSqlite(sp) || !cs.Contains("mode=memory", StringComparison.OrdinalIgnoreCase))
            {
                return new SqliteKeepAlive(null);
            }

            // A shared in-memory database lives only while one connection stays open.
            var keepAlive = new SqliteConnection(cs);
            keepAlive.Open();
            return new SqliteKeepAlive(keepAlive);
        });

        services.AddDbContext<IdentityDbContext>((sp, options) =>
        {
            var connectionString = sp.GetRequiredService<IConfiguration>().GetConnectionString("Identity")
                                   ?? throw new InvalidOperationException("ConnectionStrings:Identity is not configured.");
            if (IsSqlite(sp))
            {
                sp.GetRequiredService<SqliteKeepAlive>();
                options.UseSqlite(connectionString, sqlite => sqlite.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery));
                options.ConfigureWarnings(w => w.Ignore(new Microsoft.Extensions.Logging.EventId(30000)) /* SQLite ignores schemas: expected in dev/tests */);
            }
            else
            {
                // Aggregates load several owned collections (history, roles): split queries avoid the cartesian explosion of a single join.
                options.UseSqlServer(connectionString, sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", IdentityDbContext.Schema)
                    .UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery));
            }

            options.AddInterceptors(sp.GetRequiredService<OutboxSaveChangesInterceptor>());
            options.ReplaceService<IModelCacheKeyFactory, ProviderAwareModelCacheKeyFactory>();
        });
        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<IdentityDbContext>());
        services.AddDurableIdempotency<IdentityDbContext>();

        services.AddScoped<AccountRepository>();
        services.AddScoped<IAccountRepository>(sp => sp.GetRequiredService<AccountRepository>());
        services.AddScoped<IAccountUniquenessChecker>(sp => sp.GetRequiredService<AccountRepository>());
        services.AddScoped<IApiCredentialRepository, ApiCredentialRepository>();
        services.AddScoped<IPasswordPolicyRepository, PasswordPolicyRepository>();
        services.AddScoped<ISessionTimeoutSettingRepository, SessionTimeoutSettingRepository>();
        services.AddScoped<IRoleRepository, RoleRepository>();
        services.AddScoped<IPrivacyConsentRepository, PrivacyConsentRepository>();
        services.AddScoped<IIdentityReadStore, IdentityReadStore>();
        services.AddScoped<IAccessLog, EfAccessLog>();

        services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();
        services.AddSingleton<HmacSecretHasher>();
        services.AddSingleton<IOtpHasher>(sp => sp.GetRequiredService<HmacSecretHasher>());
        services.AddSingleton<IApiSecretHasher>(sp => sp.GetRequiredService<HmacSecretHasher>());
        services.AddSingleton<ISecretGenerator, SecretGenerator>();
        services.AddSingleton<AesGcmProtector>();
        services.AddSingleton<IMfaService, TotpMfaService>();
        services.AddSingleton<SigningKeyService>();
        services.AddSingleton<IAccessTokenService, JwtAccessTokenService>();
        services.AddSingleton<IServiceClientRegistry, ConfiguredServiceClientRegistry>();

        services.AddSingleton<ISessionStore, CacheSessionStore>();
        services.AddSingleton<IMfaChallengeStore, CacheMfaChallengeStore>();
        services.AddSingleton<ILoginCodeStore, CacheLoginCodeStore>();
        services.AddSingleton<ICacheInvalidator, CacheInvalidator>();
        services.AddScoped<IRoleDirectory, CachedRoleDirectory>();

        // Delivery adapter is chosen lazily from the final configuration (Delivery:Provider = Log|Capture).
        services.AddSingleton<CapturingMessageSender>();
        services.AddSingleton<LoggingMessageSender>();
        services.AddSingleton<IOtpSender>(sp => UseCapture(sp) ? sp.GetRequiredService<CapturingMessageSender>() : sp.GetRequiredService<LoggingMessageSender>());
        services.AddSingleton<IEmailVerificationSender>(sp => UseCapture(sp) ? sp.GetRequiredService<CapturingMessageSender>() : sp.GetRequiredService<LoggingMessageSender>());

        // Order matters: schema + seed + signing keys first, then background publishers.
        services.AddHostedService<IdentityDbInitializer>();
        services.AddHostedService<SigningKeyRotationService>();
        services.Configure<AccessLogOptions>(configuration.GetSection(AccessLogOptions.SectionName));
        services.AddSingleton<AccessLogRetentionService>();
        services.AddHostedService(sp => sp.GetRequiredService<AccessLogRetentionService>());
        services.AddOutboxProcessor<IdentityDbContext>(configuration);
        return services;
    }

    private static bool IsSqlite(IServiceProvider sp) =>
        string.Equals(sp.GetRequiredService<IConfiguration>()[$"{DatabaseOptions.SectionName}:Provider"], "Sqlite", StringComparison.OrdinalIgnoreCase);

    private static bool UseCapture(IServiceProvider sp) =>
        string.Equals(sp.GetRequiredService<IConfiguration>()[$"{DeliveryOptions.SectionName}:Provider"], "Capture", StringComparison.OrdinalIgnoreCase);
}

/// <summary>Holds the connection that keeps a shared in-memory SQLite database alive (null when not applicable).</summary>
public sealed class SqliteKeepAlive : IDisposable
{
    private readonly SqliteConnection? _connection;

    public SqliteKeepAlive(SqliteConnection? connection) => _connection = connection;

    public void Dispose() => _connection?.Dispose();
}

/// <summary>The model differs by provider (rowversion vs stamped token), so the model cache must be keyed by provider as well.</summary>
internal sealed class ProviderAwareModelCacheKeyFactory : IModelCacheKeyFactory
{
    public object Create(DbContext context, bool designTime) => (context.GetType(), context.Database.ProviderName, designTime);
}
