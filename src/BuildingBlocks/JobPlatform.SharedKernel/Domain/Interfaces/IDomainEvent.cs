namespace JobPlatform.SharedKernel.Domain.Interfaces;

public interface IDomainEvent
{
    Guid Id { get; }
    DateTime OccurredOnUtc { get; }
}
