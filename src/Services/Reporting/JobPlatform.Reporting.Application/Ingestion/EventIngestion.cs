using JobPlatform.Reporting.Application.Services.Ingestion;
using JobPlatform.SharedKernel.Messaging;

namespace JobPlatform.Reporting.Application.Ingestion;

/// <summary>Turns one event into a fact projection (handover 5.2: "per-event IFactProjector"). Projectors are idempotent and never call another BC.</summary>
public interface IFactProjector<in TEvent> where TEvent : IIntegrationEvent
{
    Task ProjectAsync(TEvent integrationEvent, CancellationToken ct);
}

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
