using JobPlatform.Reporting.Application.Commands.ReportRuns;
using JobPlatform.Reporting.Application.DTOs.ReportRuns;
using JobPlatform.Reporting.Application.Services.ReportRuns;
using JobPlatform.Reporting.Domain;
using JobPlatform.Reporting.Domain.Interfaces.Repositories;
using JobPlatform.SharedKernel.Application.Interfaces.Cqrs;
using JobPlatform.SharedKernel.Application.Interfaces.Ports;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.Reporting.Application.Handlers.ReportRuns;

internal sealed class RunSavedReportHandler : ICommandHandler<RunSavedReportCommand, ReportResultDto>
{
    private readonly ISavedReportRepository _saved;
    private readonly ReportRunner _runner;
    private readonly ICurrentUser _user;
    private readonly ReportRunService _reportRunService;

    public RunSavedReportHandler(ISavedReportRepository saved, ReportRunner runner, ICurrentUser user, ReportRunService reportRunService)
    {
        _saved = saved;
        _runner = runner;
        _user = user;
        _reportRunService = reportRunService;
    }

    public async Task<Result<ReportResultDto>> Handle(RunSavedReportCommand request, CancellationToken ct)
    {
        if (await _reportRunService.DeniedAsync(ReportCategory.Custom, nameof(RunSavedReportCommand), ct) is { } denied)
        {
            return denied;
        }

        var report = await _saved.GetAsync(request.Id, ct);
        if (report is null)
        {
            return Error.NotFound(ReportingErrorCodes.NotFound, "The saved report was not found.");
        }

        report.EnsureOwnedBy(_user.UserId!.Value);
        if (await _reportRunService.DeniedAsync(ReportCatalog.CategoryOf(report.Definition.DataSource), nameof(RunSavedReportCommand), ct) is { } deniedSource)
        {
            return deniedSource;
        }

        return await _runner.RunAsync(report.Name, report.Definition, request.Arguments, ReportRunService.TargetOf(request.Target), ct);
    }
}
