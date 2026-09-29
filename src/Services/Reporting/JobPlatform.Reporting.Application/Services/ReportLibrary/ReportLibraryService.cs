using JobPlatform.Reporting.Domain;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.Reporting.Application.Services.ReportLibrary;

/// <summary>Logic shared by the report library request handlers.</summary>
internal sealed class ReportLibraryService
{
    private readonly IReportAccessGuard _guard;
    private readonly TimeProvider _clock;

    public ReportLibraryService(IReportAccessGuard guard, TimeProvider clock)
    {
        _guard = guard;
        _clock = clock;
    }

    public DateTime Now => _clock.GetUtcNow().UtcDateTime;

    public async Task<Error?> DeniedAsync(string name, CancellationToken ct) => (await _guard.EnsureAsync(ReportCategory.Custom, name, ct)).Error;
}
