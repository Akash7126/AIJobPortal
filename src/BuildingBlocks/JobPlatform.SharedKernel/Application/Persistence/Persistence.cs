namespace JobPlatform.SharedKernel.Application.Persistence;

public interface IUnitOfWork
{
    Task BeginTransactionAsync(CancellationToken ct = default);
    Task<int> SaveChangesAsync(CancellationToken ct = default);
    Task CommitTransactionAsync(CancellationToken ct = default);
    Task RollbackTransactionAsync(CancellationToken ct = default);
}

/// <summary>Marker for aggregate repositories.</summary>
public interface IRepository<TAggregate, TId> where TAggregate : notnull where TId : notnull
{
}

public sealed class ConcurrencyConflictException : Exception
{
    public ConcurrencyConflictException(Exception inner) : base("The record was modified by another request.", inner)
    {
    }
}

public sealed class UniqueConstraintViolationException : Exception
{
    public UniqueConstraintViolationException(Exception inner) : base("A unique constraint was violated.", inner)
    {
    }
}
