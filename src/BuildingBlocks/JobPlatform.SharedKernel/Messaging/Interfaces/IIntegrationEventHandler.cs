namespace JobPlatform.SharedKernel.Messaging.Interfaces;

public interface IIntegrationEventHandler<in TEvent> where TEvent : IIntegrationEvent
{
    Task Handle(TEvent integrationEvent, CancellationToken ct);
}
