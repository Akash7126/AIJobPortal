using System.Text.Json;
using JobPlatform.Reporting.Application.DTOs.LaborMarket;
using JobPlatform.Reporting.Domain;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.Reporting.Application.Services.LaborMarket;

/// <summary>Logic shared by the labor market request handlers.</summary>
internal sealed class LaborMarketService
{
    private readonly ILaborMarketReportRepository _reports;
    private readonly LaborMarketReportBuilder _builder;
    private readonly TimeProvider _clock;

    public LaborMarketService(ILaborMarketReportRepository reports, LaborMarketReportBuilder builder, TimeProvider clock)
    {
        _reports = reports;
        _builder = builder;
        _clock = clock;
    }

    public DateTime Now => _clock.GetUtcNow().UtcDateTime;

    public string PreviousMonth() => LaborMarketReport.PeriodOf(new DateTime(Now.Year, Now.Month, 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(-1));

    public async Task<Result<LaborMarketReportDto>> GenerateAsync(string period, Guid? by, CancellationToken ct)
    {
        var existing = await _reports.GetByPeriodAsync(period, ct);
        if (existing is not null)
        {
            return ToDto(existing, true);
        }

        var report = LaborMarketReport.Generate(period, await _builder.BuildJsonAsync(period, ct), by, Now);
        _reports.Add(report);
        return ToDto(report, false);
    }

    public static LaborMarketReportDto ToDto(LaborMarketReport r, bool existing) =>
        new(r.Id, r.Period, r.GeneratedAtUtc, JsonDocument.Parse(r.ContentJson).RootElement.Clone(), existing);
}
