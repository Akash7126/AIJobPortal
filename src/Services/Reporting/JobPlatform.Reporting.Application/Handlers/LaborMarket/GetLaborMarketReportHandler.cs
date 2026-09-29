using JobPlatform.Reporting.Application.DTOs.LaborMarket;
using JobPlatform.Reporting.Application.Queries.LaborMarket;
using JobPlatform.Reporting.Application.Services.LaborMarket;
using JobPlatform.Reporting.Domain;
using JobPlatform.SharedKernel.Application.Abstractions;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.Reporting.Application.Handlers.LaborMarket;

internal sealed class GetLaborMarketReportHandler : IQueryHandler<GetLaborMarketReportQuery, LaborMarketReportDto>
{
    private readonly IReportAccessGuard _guard;
    private readonly ILaborMarketReportRepository _reports;
    private readonly LaborMarketService _laborMarketService;

    public GetLaborMarketReportHandler(IReportAccessGuard guard, ILaborMarketReportRepository reports, LaborMarketService laborMarketService)
    {
        _guard = guard;
        _reports = reports;
        _laborMarketService = laborMarketService;
    }

    public async Task<Result<LaborMarketReportDto>> Handle(GetLaborMarketReportQuery request, CancellationToken ct)
    {
        var access = await _guard.EnsureAsync(ReportCategory.EmploymentStatistics, nameof(GetLaborMarketReportQuery), ct);
        if (access.IsFailure)
        {
            return access.Error!;
        }

        var period = request.Period ?? _laborMarketService.PreviousMonth();
        var report = await _reports.GetByPeriodAsync(period, ct);
        return report is null
            ? Error.NotFound(ReportingErrorCodes.NotFound, $"No labor-market report exists for {period}.")
            : LaborMarketService.ToDto(report, true);
    }
}
