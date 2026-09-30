namespace JobPlatform.Reporting.Application.Interfaces;

/// <summary>Anti-corruption port to infrastructure telemetry (Prometheus / OTel; Q-04). Failure degrades to "no data".</summary>
public interface IMetricsSource
{
    /// <summary>Current value of every known metric the backend can supply. Empty when the backend has no data.</summary>
    Task<IReadOnlyDictionary<string, decimal>> SampleAsync(CancellationToken ct = default);
}
