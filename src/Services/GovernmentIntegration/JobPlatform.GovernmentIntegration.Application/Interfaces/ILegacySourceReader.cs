using JobPlatform.GovernmentIntegration.Domain;

namespace JobPlatform.GovernmentIntegration.Application.Interfaces;

/// <summary>Legacy MoL/PEF read for the migration saga (handover section 4.3, US-6.1-02).</summary>
public interface ILegacySourceReader
{
    Task<IReadOnlyList<LegacySourceRecord>> ReadBatchAsync(SourceSystem sourceSystem, int take, CancellationToken ct);
}
