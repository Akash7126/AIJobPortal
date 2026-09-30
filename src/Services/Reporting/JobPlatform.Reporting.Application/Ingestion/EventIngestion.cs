using JobPlatform.Reporting.Application.Interfaces.Ingestion;
using JobPlatform.Reporting.Application.Services.Ingestion;
using JobPlatform.SharedKernel.Messaging.Interfaces;

namespace JobPlatform.Reporting.Application.Ingestion;

/// <summary>The single inbox handler registered for every consumed event: ingest, then run the projectors of that event.</summary>
public sealed class IngestionHandler<TEvent> : IIntegrationEventHandler<TEvent> where TEvent : IIntegrationEvent
{
    private readonly EventIngestionService _ingestion;
    private readonly IEnumerable<IFactProjector<TEvent>> _projectors;

    public IngestionHandler(EventIngestionService ingestion, IEnumerable<IFactProjector<TEvent>> projectors)
    {
        _ingestion = ingestion;
        _projectors = projectors;
    }

    public async Task Handle(TEvent integrationEvent, CancellationToken ct)
    {
        if (!await _ingestion.RecordAsync(integrationEvent, ct))
        {
            return;
        }

        foreach (var projector in _projectors)
        {
            await projector.ProjectAsync(integrationEvent, ct);
        }
    }
}
