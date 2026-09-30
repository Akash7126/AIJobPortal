using JobPlatform.Reporting.Application.DTOs.ReportRuns;
using JobPlatform.Reporting.Application.Queries.ReportRuns;
using JobPlatform.Reporting.Application.Services.ReportRuns;
using JobPlatform.SharedKernel.Application.Interfaces.Cqrs;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.Reporting.Application.Handlers.ReportRuns;

internal sealed class RenderReportHandler : IQueryHandler<RenderReportQuery, ReportViewDto>
{
    private readonly ReportEngine _engine;
    private readonly ReportViewBuilder _views;
    private readonly ReportRunService _reportRunService;

    public RenderReportHandler(ReportEngine engine, ReportViewBuilder views, ReportRunService reportRunService)
    {
        _engine = engine;
        _views = views;
        _reportRunService = reportRunService;
    }

    public async Task<Result<ReportViewDto>> Handle(RenderReportQuery request, CancellationToken ct)
    {
        var resolved = await _reportRunService.ResolveAsync(request.TemplateId, request.Definition, request.Arguments, nameof(RenderReportQuery), ct);
        if (resolved.IsFailure)
        {
            return resolved.Error!;
        }

        var table = await _engine.RunAsync(resolved.Value.Definition, resolved.Value.Arguments, ct);
        return _views.Build(resolved.Value.Title, table, request.Format);
    }
}
