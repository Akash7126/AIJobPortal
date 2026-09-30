using JobPlatform.Reporting.Application.Interfaces;
using JobPlatform.Reporting.Domain;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.Reporting.Application.Services.Employment;

/// <summary>Logic shared by the employment request handlers.</summary>
internal sealed class EmploymentService
{
    private readonly IReportAccessGuard _guard;

    public EmploymentService(IReportAccessGuard guard) => _guard = guard;

    public const int Max = 200_000;

    public Task<Result<Unit2>> Guard(string name, CancellationToken ct) => GuardAsync(name, ct);

    public async Task<Result<Unit2>> GuardAsync(string name, CancellationToken ct)
    {
        var access = await _guard.EnsureAsync(ReportCategory.EmploymentStatistics, name, ct);
        return access.IsFailure ? access.Error! : Unit2.Value;
    }
}
