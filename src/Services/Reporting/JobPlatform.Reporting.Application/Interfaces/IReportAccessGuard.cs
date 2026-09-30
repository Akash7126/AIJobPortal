using JobPlatform.Reporting.Domain;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.Reporting.Application.Interfaces;

/// <summary>Per-request category access decision (US-3.5.4-08): applies the rules of the caller's roles and records the decision.</summary>
public interface IReportAccessGuard
{
    Task<Result<Unit>> EnsureAsync(ReportCategory category, string requestName, CancellationToken ct = default);
}
