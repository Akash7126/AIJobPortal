using JobPlatform.SharedKernel.Messaging.Interfaces;

namespace JobPlatform.Reporting.Application.Interfaces.Ingestion;

/// <summary>Turns one event into a fact projection (handover 5.2: "per-event IFactProjector"). Projectors are idempotent and never call another BC.</summary>
public interface IFactProjector<in TEvent> where TEvent : IIntegrationEvent
{
    Task ProjectAsync(TEvent integrationEvent, CancellationToken ct);
}
