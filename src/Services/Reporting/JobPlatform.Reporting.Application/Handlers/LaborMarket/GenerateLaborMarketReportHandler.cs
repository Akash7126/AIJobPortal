using JobPlatform.Reporting.Application.Commands.LaborMarket;
using JobPlatform.Reporting.Application.DTOs.LaborMarket;
using JobPlatform.Reporting.Application.Services.LaborMarket;
using JobPlatform.Reporting.Domain;
using JobPlatform.SharedKernel.Application.Abstractions;
using JobPlatform.SharedKernel.Application.Ports;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.Reporting.Application.Handlers.LaborMarket;

internal sealed class GenerateLaborMarketReportHandler : ICommandHandler<GenerateLaborMarketReportCommand, LaborMarketReportDto>
{
    private readonly IReportAccessGuard _guard;
    private readonly ICurrentUser _user;
    private readonly LaborMarketService _laborMarketService;

    public GenerateLaborMarketReportHandler(IReportAccessGuard guard, ICurrentUser user, LaborMarketService laborMarketService)
    {
        _guard = guard;
        _user = user;
        _laborMarketService = laborMarketService;
    }

    public async Task<Result<LaborMarketReportDto>> Handle(GenerateLaborMarketReportCommand request, CancellationToken ct)
    {
        var access = await _guard.EnsureAsync(ReportCategory.EmploymentStatistics, nameof(GenerateLaborMarketReportCommand), ct);
        if (access.IsFailure)
        {
            return access.Error!;
        }

        return await _laborMarketService.GenerateAsync(request.Period ?? _laborMarketService.PreviousMonth(), _user.UserId, ct);
    }
}
