using JobPlatform.Reporting.Application.Commands.ReportRuns;
using JobPlatform.Reporting.Application.DTOs.ReportRuns;
using JobPlatform.Reporting.Application.Services.ReportRuns;
using JobPlatform.SharedKernel.Application.Abstractions;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.Reporting.Application.Handlers.ReportRuns;

internal sealed class RunCustomReportHandler : ICommandHandler<RunCustomReportCommand, ReportResultDto>
{
    private readonly ReportRunner _runner;
    private readonly ReportRunService _reportRunService;

    public RunCustomReportHandler(ReportRunner runner, ReportRunService reportRunService)
    {
        _runner = runner;
        _reportRunService = reportRunService;
    }

    public async Task<Result<ReportResultDto>> Handle(RunCustomReportCommand request, CancellationToken ct)
    {
        var resolved = await _reportRunService.ResolveAsync(request.TemplateId, request.Definition, request.Arguments, nameof(RunCustomReportCommand), ct);
        return resolved.IsFailure
            ? resolved.Error!
            : await _runner.RunAsync(resolved.Value.Title, resolved.Value.Definition, resolved.Value.Arguments, ReportRunService.TargetOf(request.Target), ct);
    }
}
