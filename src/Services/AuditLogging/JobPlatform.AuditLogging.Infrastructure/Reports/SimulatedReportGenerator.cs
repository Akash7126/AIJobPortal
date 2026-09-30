using JobPlatform.AuditLogging.Application.Interfaces;
using JobPlatform.AuditLogging.Domain;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.AuditLogging.Infrastructure.Reports;

/// <summary>
/// Deterministic stand-in for the BC-12 report API (anti-corruption adapter behind <see cref="IReportGenerator"/>). It returns a stable reference for the same
/// report type, format and parameters so the export workflow (Queued, Generating, Ready) can be exercised end to end without BC-12 running.
/// A parameter "simulateFailure" = "true" makes it fail, which is how the Failed path is tested.
/// </summary>
public sealed class SimulatedReportGenerator : IReportGenerator
{
    public Task<Result<string>> GenerateAsync(ReportType type, ExportFormat format, IReadOnlyDictionary<string, string> parameters, CancellationToken ct = default)
    {
        if (parameters.TryGetValue("simulateFailure", out var flag) && flag == "true")
        {
            return Task.FromResult<Result<string>>(Error.External("E-AUDIT-REPORT-SOURCE-UNAVAILABLE", "The report source is unavailable."));
        }

        var hash = ExportJob.HashParameters(type, format, parameters)[..16].ToLowerInvariant();
        return Task.FromResult(Result.Success($"simulated://reports/{type.ToString().ToLowerInvariant()}/{hash}.{format.ToString().ToLowerInvariant()}"));
    }
}
