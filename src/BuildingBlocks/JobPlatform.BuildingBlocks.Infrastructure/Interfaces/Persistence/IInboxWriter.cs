using JobPlatform.BuildingBlocks.Infrastructure.Persistence;

namespace JobPlatform.BuildingBlocks.Infrastructure.Interfaces.Persistence;

public interface IInboxWriter
{
    /// <summary>Stores the message. Returns false when (MessageId, ConsumerName) already exists (duplicate delivery - ack and skip).</summary>
    Task<bool> TryAddAsync(InboxMessage message, CancellationToken ct = default);
}
