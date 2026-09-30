using JobPlatform.AccountIdentity.Application.Interfaces;
using JobPlatform.AccountIdentity.Domain.Accounts;
using JobPlatform.AccountIdentity.Domain.PasswordPolicies;
using JobPlatform.AccountIdentity.Domain.Rbac;
using JobPlatform.AccountIdentity.Domain.Sessions;
using JobPlatform.AccountIdentity.Infrastructure.Security;
using JobPlatform.SharedKernel.Common.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace JobPlatform.AccountIdentity.Infrastructure.Persistence;

public sealed class DatabaseOptions
{
    public const string SectionName = "Database";

    /// <summary>"SqlServer" or "Sqlite" (local development and tests).</summary>
    public string Provider { get; set; } = "SqlServer";

    /// <summary>Apply EF migrations at startup (SQL Server). Production uses a migration job instead (foundation section 8).</summary>
    public bool ApplyMigrationsOnStartup { get; set; }
}

/// <summary>First bootstrap administrator (there is no public admin registration). Set through secrets/environment.</summary>
public sealed class BootstrapOptions
{
    public const string SectionName = "Bootstrap";

    public string AdminEmail { get; set; } = string.Empty;
    public string AdminMobile { get; set; } = string.Empty;
    public string AdminName { get; set; } = "Platform Administrator";
    public string AdminPassword { get; set; } = string.Empty;
}

/// <summary>
/// Startup hosted service: prepares the schema (migrate on SQL Server when enabled, EnsureCreated on SQLite), seeds built-in roles,
/// the singleton settings and the bootstrap administrator, then loads the JWT signing keys. Idempotent.
/// </summary>
public sealed class IdentityDbInitializer : IHostedService
{
    private readonly IServiceProvider _services;
    private readonly DatabaseOptions _database;
    private readonly BootstrapOptions _bootstrap;
    private readonly SigningKeyService _signingKeys;
    private readonly ILogger<IdentityDbInitializer> _logger;

    public IdentityDbInitializer(IServiceProvider services, IOptions<DatabaseOptions> database, IOptions<BootstrapOptions> bootstrap,
        SigningKeyService signingKeys, ILogger<IdentityDbInitializer> logger)
    {
        _services = services;
        _database = database.Value;
        _bootstrap = bootstrap.Value;
        _signingKeys = signingKeys;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = _services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var clock = scope.ServiceProvider.GetRequiredService<TimeProvider>();

        if (db.IsSqlite)
        {
            // The generated migrations target SQL Server; SQLite (dev/tests) builds the schema from the model.
            await db.Database.EnsureCreatedAsync(cancellationToken);
        }
        else if (_database.ApplyMigrationsOnStartup)
        {
            await db.Database.MigrateAsync(cancellationToken);
        }

        await SeedAsync(db, scope.ServiceProvider, clock, cancellationToken);
        await _signingKeys.EnsureActiveKeyAsync(cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private async Task SeedAsync(IdentityDbContext db, IServiceProvider services, TimeProvider clock, CancellationToken ct)
    {
        var existingRoles = (await db.Roles.Select(r => r.Id).ToListAsync(ct)).ToHashSet();
        foreach (var (id, name, permissions) in WellKnownRoles.Definitions)
        {
            if (!existingRoles.Contains(id))
            {
                db.Roles.Add(Role.Create(id, name, isSystem: true, permissions));
            }
        }

        if (!await db.PasswordPolicies.AnyAsync(ct))
        {
            db.PasswordPolicies.Add(PasswordPolicy.CreateDefault(clock));
        }

        if (!await db.SessionSettings.AnyAsync(ct))
        {
            db.SessionSettings.Add(SessionTimeoutSetting.CreateDefault(clock));
        }

        await db.SaveChangesAsync(ct);
        await SeedAdministratorAsync(db, services, clock, ct);
    }

    private async Task SeedAdministratorAsync(IdentityDbContext db, IServiceProvider services, TimeProvider clock, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_bootstrap.AdminEmail) || string.IsNullOrWhiteSpace(_bootstrap.AdminPassword))
        {
            _logger.LogInformation("No bootstrap administrator configured; skipping");
            return;
        }

        if (!Email.TryCreate(_bootstrap.AdminEmail, out var email) || !MobileNumber.TryCreate(_bootstrap.AdminMobile, out var mobile))
        {
            _logger.LogWarning("Bootstrap administrator e-mail/mobile are invalid; skipping");
            return;
        }

        if (await db.Accounts.AnyAsync(a => a.Email == email && a.ActorType == JobPlatform.SharedKernel.Common.Enums.ActorType.Administrator, ct))
        {
            return;
        }

        var policy = await db.PasswordPolicies.FirstAsync(p => p.Id == PasswordPolicy.SingletonId, ct);
        policy.EnsureCompliant(_bootstrap.AdminPassword);
        var hasher = services.GetRequiredService<IPasswordHasher>();
        var admin = Account.CreateAdministrator(_bootstrap.AdminName, email!, mobile!, new PasswordHash(hasher.Hash(_bootstrap.AdminPassword)),
            policy.PolicyVersion, clock);
        db.Accounts.Add(admin);
        await db.SaveChangesAsync(ct);
        _logger.LogInformation("Bootstrap administrator {AccountId} created (MFA enrolment is required at first sign-in)", admin.Id.Value);
    }
}
