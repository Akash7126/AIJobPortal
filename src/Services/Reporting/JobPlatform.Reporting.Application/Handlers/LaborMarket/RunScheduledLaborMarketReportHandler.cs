using JobPlatform.Reporting.Application.Commands.LaborMarket;
using JobPlatform.Reporting.Application.DTOs.LaborMarket;
using JobPlatform.Reporting.Application.Services.LaborMarket;
using JobPlatform.SharedKernel.Application.Abstractions;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.Reporting.Application.Handlers.LaborMarket;

internal sealed class RunScheduledLaborMarketReportHandler : ICommandHandler<RunScheduledLaborMarketReportCommand, LaborMarketReportDto>
{
    private readonly LaborMarketService _laborMarketService;

    public RunScheduledLaborMarketReportHandler(LaborMarketService laborMarketService) => _laborMarketService = laborMarketService;

    public Task<Result<LaborMarketReportDto>> Handle(RunScheduledLaborMarketReportCommand request, CancellationToken ct) => _laborMarketService.GenerateAsync(_laborMarketService.PreviousMonth(), null, ct);
}
