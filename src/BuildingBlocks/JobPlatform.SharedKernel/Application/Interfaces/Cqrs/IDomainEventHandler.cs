using JobPlatform.SharedKernel.Domain.Interfaces;

namespace JobPlatform.SharedKernel.Application.Interfaces.Cqrs;

public interface IDomainEventHandler<in TEvent> where TEvent : IDomainEvent
{
    Task Handle(TEvent domainEvent, CancellationToken ct);
}
