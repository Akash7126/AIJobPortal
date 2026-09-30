using JobPlatform.SharedKernel.Domain.Interfaces;

namespace JobPlatform.BuildingBlocks.Infrastructure.Interfaces.Persistence;

public interface IDomainEventDispatcher
{
    Task DispatchAsync(IReadOnlyCollection<IDomainEvent> events, CancellationToken ct = default);
}
