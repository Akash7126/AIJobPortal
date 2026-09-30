namespace JobPlatform.SharedKernel.Domain.Interfaces;

/// <summary>Non-generic view of an aggregate root used by infrastructure (outbox interceptor).</summary>
public interface IAggregateRoot
{
    string AggregateId { get; }
    long Version { get; }
    IReadOnlyCollection<IDomainEvent> DomainEvents { get; }
    void ClearDomainEvents();
}
