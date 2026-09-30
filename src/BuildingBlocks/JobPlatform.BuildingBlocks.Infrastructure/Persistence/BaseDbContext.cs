using System.Reflection;
using JobPlatform.SharedKernel.Application.Interfaces.Persistence;
using JobPlatform.SharedKernel.Application.Persistence;
using JobPlatform.SharedKernel.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace JobPlatform.BuildingBlocks.Infrastructure.Persistence;

/// <summary>Base for every BC DbContext: unit of work, UTC datetimes, outbox/inbox tables, provider-aware row versions, error translation.</summary>
public abstract class BaseDbContext : DbContext, IUnitOfWork
{
    public const string MessagingSchema = "messaging";
    private const string SqliteProvider = "Microsoft.EntityFrameworkCore.Sqlite";

    protected BaseDbContext(DbContextOptions options) : base(options)
    {
    }

    public bool IsSqlite => Database.ProviderName == SqliteProvider;

    public async Task BeginTransactionAsync(CancellationToken ct = default)
    {
        if (Database.CurrentTransaction is null)
        {
            await Database.BeginTransactionAsync(ct);
        }
    }

    public async Task CommitTransactionAsync(CancellationToken ct = default)
    {
        if (Database.CurrentTransaction is { } transaction)
        {
            await transaction.CommitAsync(ct);
            await transaction.DisposeAsync();
        }
    }

    public async Task RollbackTransactionAsync(CancellationToken ct = default)
    {
        if (Database.CurrentTransaction is { } transaction)
        {
            await transaction.RollbackAsync(ct);
            await transaction.DisposeAsync();
        }
    }

    public override async Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        StampSqliteRowVersions();
        try
        {
            return await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            throw new ConcurrencyConflictException(ex);
        }
        catch (DbUpdateException ex) when (PersistenceErrors.IsUniqueViolation(ex))
        {
            throw new UniqueConstraintViolationException(ex);
        }
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        StampSqliteRowVersions();
        try
        {
            return base.SaveChanges(acceptAllChangesOnSuccess);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            throw new ConcurrencyConflictException(ex);
        }
        catch (DbUpdateException ex) when (PersistenceErrors.IsUniqueViolation(ex))
        {
            throw new UniqueConstraintViolationException(ex);
        }
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // Time is always UTC (foundation section 8); SQL Server datetime2 does not store the Kind, so restore it on read.
        configurationBuilder.Properties<DateTime>().HaveConversion<UtcDateTimeConverter>();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyMessagingConfiguration();
        base.OnModelCreating(modelBuilder);
    }

    private void StampSqliteRowVersions()
    {
        if (!IsSqlite)
        {
            return;
        }

        foreach (var entry in ChangeTracker.Entries<IAggregateRoot>().Where(e => e.State is EntityState.Added or EntityState.Modified))
        {
            entry.Property("RowVersion").CurrentValue = Guid.NewGuid().ToByteArray();
        }
    }

    private sealed class UtcDateTimeConverter : ValueConverter<DateTime, DateTime>
    {
        public UtcDateTimeConverter() : base(v => v.Kind == DateTimeKind.Utc ? v : v.ToUniversalTime(), v => DateTime.SpecifyKind(v, DateTimeKind.Utc))
        {
        }
    }
}

public static class AggregateMapping
{
    /// <summary>
    /// Maps the shared aggregate columns: optimistic-concurrency token (rowversion on SQL Server, app-stamped bytes on SQLite)
    /// and the per-aggregate event counter as AggregateVersion.
    /// </summary>
    public static void ConfigureAggregate<TAggregate>(this EntityTypeBuilder<TAggregate> builder, bool isSqlite) where TAggregate : class, IAggregateRoot
    {
        var rowVersion = builder.Property<byte[]>("RowVersion");
        if (isSqlite)
        {
            rowVersion.IsConcurrencyToken();
        }
        else
        {
            rowVersion.IsRowVersion();
        }

        builder.Property<long>("Version").HasColumnName("AggregateVersion");
        builder.Ignore(a => a.DomainEvents);
        builder.Ignore(a => a.AggregateId);
    }
}

internal static class PersistenceErrors
{
    public static bool IsUniqueViolation(DbUpdateException exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            var typeName = current.GetType().Name;
            if (typeName == "SqlException")
            {
                var number = current.GetType().GetProperty("Number", BindingFlags.Public | BindingFlags.Instance)?.GetValue(current) as int?;
                if (number is 2601 or 2627)
                {
                    return true;
                }
            }
            else if (typeName == "SqliteException")
            {
                // Extended result codes: 1555 = SQLITE_CONSTRAINT_PRIMARYKEY, 2067 = SQLITE_CONSTRAINT_UNIQUE. The code is reliable even when
                // concurrent use of one connection leaves only a generic message.
                var extended = current.GetType().GetProperty("SqliteExtendedErrorCode", BindingFlags.Public | BindingFlags.Instance)?.GetValue(current) as int?;
                if (extended is 1555 or 2067 || current.Message.Contains("UNIQUE constraint failed", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }

        return false;
    }
}
