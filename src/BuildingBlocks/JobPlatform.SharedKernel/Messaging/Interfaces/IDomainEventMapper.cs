using JobPlatform.SharedKernel.Domain.Interfaces;

namespace JobPlatform.SharedKernel.Messaging.Interfaces;

/// <summary>Maps a domain event to the integration event other BCs consume (null = not published outside the BC).</summary>
public interface IDomainEventMapper
{
    IIntegrationEvent? Map(IDomainEvent domainEvent, DomainEventContext context);
}
