using JobPlatform.Reporting.Application.DTOs.Common;

namespace JobPlatform.Reporting.Application.Interfaces;

/// <summary>Anti-corruption port to Microsoft Power BI (Q-10). Implementations must honour the cancellation token (the caller applies the 30 s timeout).</summary>
public interface IPowerBiExporter
{
    Task<PowerBiPublication> PublishAsync(string reportName, ReportTable table, CancellationToken ct = default);
}
