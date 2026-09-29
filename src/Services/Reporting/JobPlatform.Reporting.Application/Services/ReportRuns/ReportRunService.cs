using JobPlatform.Reporting.Application.DTOs.Common;
using JobPlatform.Reporting.Application.DTOs.ReportRuns;
using JobPlatform.Reporting.Domain;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.Reporting.Application.Services.ReportRuns;

/// <summary>Logic shared by the report run request handlers.</summary>
internal sealed class ReportRunService
{
    private readonly IReportAccessGuard _guard;
    private readonly IReportTemplateRepository _templates;

    public ReportRunService(IReportAccessGuard guard, IReportTemplateRepository templates)
    {
        _guard = guard;
        _templates = templates;
    }

    public async Task<Error?> DeniedAsync(ReportCategory category, string name, CancellationToken ct) => (await _guard.EnsureAsync(category, name, ct)).Error;

    /// <summary>Resolves a template or an ad-hoc definition to a validated definition plus arguments; also checks the data source's category.</summary>
    public async Task<Result<(string Title, ReportDefinition Definition, IReadOnlyDictionary<string, string> Arguments)>> ResolveAsync(Guid? templateId, ReportDefinitionDto? dto,
        IReadOnlyDictionary<string, string>? arguments, string requestName, CancellationToken ct)
    {
        if (templateId is { } id)
        {
            var template = await _templates.GetAsync(id, ct);
            if (template is null)
            {
                return Error.NotFound(ReportingErrorCodes.NotFound, "The template was not found.");
            }

            var resolved = template.ResolveArguments(arguments);
            if (await DeniedAsync(ReportCatalog.CategoryOf(template.DataSource), requestName, ct) is { } deniedTemplate)
            {
                return deniedTemplate;
            }

            var withRange = new Dictionary<string, string>(resolved, StringComparer.OrdinalIgnoreCase);
            return (template.Name, ReportMapping.DefinitionOf(template, resolved).ValidateAndNormalise(), withRange);
        }

        var definition = ReportMapping.ToDomain(dto!).ValidateAndNormalise();
        if (await DeniedAsync(ReportCatalog.CategoryOf(definition.DataSource), requestName, ct) is { } denied)
        {
            return denied;
        }

        return ("Custom report", definition, arguments ?? new Dictionary<string, string>());
    }

    public static ReportTarget TargetOf(string? target) => target is null ? ReportTarget.Builtin : Enum.Parse<ReportTarget>(target, true);

    public static AccessRuleDto ToDto(ReportAccessRule r) => new(r.Role, r.Categories.Select(c => c.ToString()).ToList(), r.UpdatedAtUtc);
}
