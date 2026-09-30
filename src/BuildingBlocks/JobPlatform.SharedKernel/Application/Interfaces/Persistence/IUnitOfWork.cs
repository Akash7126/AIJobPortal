namespace JobPlatform.SharedKernel.Application.Interfaces.Persistence;

public interface IUnitOfWork
{
    Task BeginTransactionAsync(CancellationToken ct = default);
    Task<int> SaveChangesAsync(CancellationToken ct = default);
    Task CommitTransactionAsync(CancellationToken ct = default);
    Task RollbackTransactionAsync(CancellationToken ct = default);
}
