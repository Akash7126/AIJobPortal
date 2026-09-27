using JobPlatform.BuildingBlocks.Infrastructure.Persistence;
using JobPlatform.EmployerOnboarding.Domain;
using Microsoft.EntityFrameworkCore;

namespace JobPlatform.EmployerOnboarding.Infrastructure.Persistence;

public sealed class EmployerOnboardingDbContext : BaseDbContext
{
    public const string Schema = "employer";

    public EmployerOnboardingDbContext(DbContextOptions<EmployerOnboardingDbContext> options) : base(options)
    {
    }

    public DbSet<EmployerRegistration> EmployerRegistrations => Set<EmployerRegistration>();
    public DbSet<CompanyMediaAndDocument> CompanyMedia => Set<CompanyMediaAndDocument>();
    public DbSet<EmployerStanding> EmployerStandings => Set<EmployerStanding>();
    public DbSet<KnownAccount> KnownAccounts => Set<KnownAccount>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfiguration(new EmployerRegistrationConfiguration(IsSqlite));
        modelBuilder.ApplyConfiguration(new CompanyMediaAndDocumentConfiguration(IsSqlite));
        modelBuilder.ApplyConfiguration(new EmployerStandingConfiguration(IsSqlite));
        modelBuilder.ApplyConfiguration(new BadgeAuditEntryConfiguration());
        modelBuilder.ApplyConfiguration(new KnownAccountConfiguration());
        base.OnModelCreating(modelBuilder);
    }
}
