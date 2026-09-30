using JobPlatform.Reporting.Application.Commands.ReportRuns;
using JobPlatform.Reporting.Application.DTOs.Common;
using JobPlatform.Reporting.Application.Services.ReportRuns;
using JobPlatform.Reporting.Domain;
using JobPlatform.SharedKernel.Application.Interfaces.Cqrs;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.Reporting.Application.Handlers.ReportRuns;

internal sealed class BuildReportDefinitionHandler : ICommandHandler<BuildReportDefinitionCommand, ReportDefinitionDto>
{
    private readonly ReportRunService _reportRunService;

    public BuildReportDefinitionHandler(ReportRunService reportRunService) => _reportRunService = reportRunService;

    public async Task<Result<ReportDefinitionDto>> Handle(BuildReportDefinitionCommand request, CancellationToken ct)
    {
        var definition = ReportMapping.ToDomain(request.Definition);
        var category = Enum.IsDefined(definition.DataSource) ? ReportCatalog.CategoryOf(definition.DataSource) : ReportCategory.Custom;
        if (await _reportRunService.DeniedAsync(category, nameof(BuildReportDefinitionCommand), ct) is { } denied)
        {
            return denied;
        }

        return ReportMapping.ToDto(definition.ValidateAndNormalise());
    }
}
