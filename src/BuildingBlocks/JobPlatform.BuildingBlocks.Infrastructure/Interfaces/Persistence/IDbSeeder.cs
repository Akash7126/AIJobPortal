using Microsoft.EntityFrameworkCore;

namespace JobPlatform.BuildingBlocks.Infrastructure.Interfaces.Persistence;

/// <summary>Seeds reference data after the schema exists. Must be idempotent.</summary>
public interface IDbSeeder<in TContext> where TContext : DbContext
{
    Task SeedAsync(TContext db, CancellationToken ct);
}
