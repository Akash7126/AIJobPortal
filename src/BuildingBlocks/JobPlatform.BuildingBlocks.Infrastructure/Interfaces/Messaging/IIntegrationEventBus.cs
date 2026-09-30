using JobPlatform.BuildingBlocks.Infrastructure.Messaging;

namespace JobPlatform.BuildingBlocks.Infrastructure.Interfaces.Messaging;

public interface IIntegrationEventBus
{
    /// <summary>Publishes and returns only after the broker confirmed the message (throws otherwise so the outbox retries).</summary>
    Task PublishAsync(OutboundMessage message, CancellationToken ct = default);
}
