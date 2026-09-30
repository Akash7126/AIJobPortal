using JobPlatform.AuditLogging.Domain;

namespace JobPlatform.AuditLogging.Application.Interfaces;

/// <summary>Anti-corruption port to the report source (BC-12, 3.1.4-10). The adapter is chosen by configuration (Reports:Provider = Simulated | Http).</summary>
public interface IReportGenerator
{
    /// <summary>Generates the report and returns a reference to the result (a signed link), or a failure.</summary>
    Task<JobPlatform.SharedKernel.Application.Results.Result<string>> GenerateAsync(ReportType type, ExportFormat format,
        IReadOnlyDictionary<string, string> parameters, CancellationToken ct = default);
}
