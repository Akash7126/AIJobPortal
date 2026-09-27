using FluentValidation;
using JobPlatform.Reporting.Domain;
using JobPlatform.SharedKernel.Application.Abstractions;
using JobPlatform.SharedKernel.Application.Ports;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.Reporting.Application;

// US-3.5.4-01 run, -03 formats/render, -06 builder, -07 run a saved report, -08 access rules.

/// <summary>Run a template or an ad-hoc definition (exactly one). Target: builtin or powerbi (falls back to builtin with E-CRG-UPSTREAM-TIMEOUT).</summary>
public sealed record RunCustomReportCommand(Guid? TemplateId, ReportDefinitionDto? Definition, IReadOnlyDictionary<string, string>? Arguments, string? Target)
    : CustomCommandRequest, ICommand<ReportResultDto>;

public sealed record RunSavedReportCommand(Guid Id, IReadOnlyDictionary<string, string>? Arguments, string? Target) : CustomCommandRequest, ICommand<ReportResultDto>;

public sealed record GetReportFormatsQuery : CustomRequest, IQuery<IReadOnlyList<string>>;

public sealed record RenderReportQuery(Guid? TemplateId, ReportDefinitionDto? Definition, IReadOnlyDictionary<string, string>? Arguments, string Format)
    : CustomRequest, IQuery<ReportViewDto>;

public sealed record BuildReportDefinitionCommand(ReportDefinitionDto Definition) : CustomCommandRequest, ICommand<ReportDefinitionDto>;

public sealed record GetReportBuilderFieldsQuery(string DataSource) : CustomRequest, IQuery<IReadOnlyList<BuilderFieldDto>>;

public sealed record ListAccessRulesQuery : AccessAdminRequest, IQuery<IReadOnlyList<AccessRuleDto>>;

/// <summary>Sets the categories of a role (created when missing). Takes effect on the next request of every holder of the role.</summary>
public sealed record ConfigureReportAccessCommand(string Role, IReadOnlyList<string> Categories) : AccessAdminRequest, ICommand<AccessRuleDto>;

public sealed class RunCustomReportValidator : AbstractValidator<RunCustomReportCommand>
{
    public RunCustomReportValidator()
    {
        RuleFor(x => x).Must(x => (x.TemplateId is null) != (x.Definition is null)).OverridePropertyName("definition").WithErrorCode("VAL.Definition.ExactlyOne");
        RuleFor(x => x.Target).Must(t => t is null || Enum.TryParse<ReportTarget>(t, true, out _)).WithErrorCode("VAL.Target.Unknown");
    }
}

public sealed class RunSavedReportValidator : AbstractValidator<RunSavedReportCommand>
{
    public RunSavedReportValidator() =>
        RuleFor(x => x.Target).Must(t => t is null || Enum.TryParse<ReportTarget>(t, true, out _)).WithErrorCode("VAL.Target.Unknown");
}

public sealed class RenderReportValidator : AbstractValidator<RenderReportQuery>
{
    public RenderReportValidator()
    {
        RuleFor(x => x.Format).Must(ReportViewBuilder.IsKnown).WithErrorCode("VAL.Format.Unknown");
        RuleFor(x => x).Must(x => (x.TemplateId is null) != (x.Definition is null)).OverridePropertyName("definition").WithErrorCode("VAL.Definition.ExactlyOne");
    }
}

/// <summary>BuildReportDefinitionValidator: at most 30 fields; the rest (whitelist, typed filters) is the definition's own validation.</summary>
public sealed class BuildReportDefinitionValidator : AbstractValidator<BuildReportDefinitionCommand>
{
    public BuildReportDefinitionValidator()
    {
        RuleFor(x => x.Definition).NotNull().WithErrorCode("VAL.Definition.Required");
        RuleFor(x => x.Definition.Fields).Must(f => f is null || f.Count <= ReportDefinition.MaxFields).When(x => x.Definition is not null).WithErrorCode("VAL.Fields.TooMany");
    }
}

public sealed class GetReportBuilderFieldsValidator : AbstractValidator<GetReportBuilderFieldsQuery>
{
    public GetReportBuilderFieldsValidator() =>
        RuleFor(x => x.DataSource).Must(s => Enum.TryParse<ReportDataSource>(s, true, out _)).WithErrorCode("VAL.DataSource.Unknown");
}

public sealed class ConfigureReportAccessValidator : AbstractValidator<ConfigureReportAccessCommand>
{
    public ConfigureReportAccessValidator()
    {
        RuleFor(x => x.Role).NotEmpty().WithErrorCode("VAL.Role.Required")
            .Must(r => r is null || r.Equals(ReportAccessRule.AdministratorRole, StringComparison.OrdinalIgnoreCase) || Guid.TryParse(r, out _)).WithErrorCode("VAL.Role.Unknown");
        RuleFor(x => x.Categories).NotNull().WithErrorCode("VAL.Categories.Required");
        RuleForEach(x => x.Categories).Must(c => Enum.TryParse<ReportCategory>(c, true, out _)).WithErrorCode("VAL.Category.Unknown");
    }
}

internal sealed class ReportRunHandlers :
    ICommandHandler<RunCustomReportCommand, ReportResultDto>,
    ICommandHandler<RunSavedReportCommand, ReportResultDto>,
    IQueryHandler<GetReportFormatsQuery, IReadOnlyList<string>>,
    IQueryHandler<RenderReportQuery, ReportViewDto>,
    ICommandHandler<BuildReportDefinitionCommand, ReportDefinitionDto>,
    IQueryHandler<GetReportBuilderFieldsQuery, IReadOnlyList<BuilderFieldDto>>,
    IQueryHandler<ListAccessRulesQuery, IReadOnlyList<AccessRuleDto>>,
    ICommandHandler<ConfigureReportAccessCommand, AccessRuleDto>
{
    private readonly IReportAccessGuard _guard;
    private readonly IReportTemplateRepository _templates;
    private readonly ISavedReportRepository _saved;
    private readonly IReportAccessRuleRepository _rules;
    private readonly ReportRunner _runner;
    private readonly ReportEngine _engine;
    private readonly ReportViewBuilder _views;
    private readonly ICurrentUser _user;
    private readonly TimeProvider _clock;

    public ReportRunHandlers(IReportAccessGuard guard, IReportTemplateRepository templates, ISavedReportRepository saved, IReportAccessRuleRepository rules, ReportRunner runner,
        ReportEngine engine, ReportViewBuilder views, ICurrentUser user, TimeProvider clock)
    {
        _guard = guard;
        _templates = templates;
        _saved = saved;
        _rules = rules;
        _runner = runner;
        _engine = engine;
        _views = views;
        _user = user;
        _clock = clock;
    }

    private async Task<Error?> DeniedAsync(ReportCategory category, string name, CancellationToken ct) => (await _guard.EnsureAsync(category, name, ct)).Error;

    /// <summary>Resolves a template or an ad-hoc definition to a validated definition plus arguments; also checks the data source's category.</summary>
    private async Task<Result<(string Title, ReportDefinition Definition, IReadOnlyDictionary<string, string> Arguments)>> ResolveAsync(Guid? templateId, ReportDefinitionDto? dto,
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

    private static ReportTarget TargetOf(string? target) => target is null ? ReportTarget.Builtin : Enum.Parse<ReportTarget>(target, true);

    public async Task<Result<ReportResultDto>> Handle(RunCustomReportCommand request, CancellationToken ct)
    {
        var resolved = await ResolveAsync(request.TemplateId, request.Definition, request.Arguments, nameof(RunCustomReportCommand), ct);
        return resolved.IsFailure
            ? resolved.Error!
            : await _runner.RunAsync(resolved.Value.Title, resolved.Value.Definition, resolved.Value.Arguments, TargetOf(request.Target), ct);
    }

    public async Task<Result<ReportResultDto>> Handle(RunSavedReportCommand request, CancellationToken ct)
    {
        if (await DeniedAsync(ReportCategory.Custom, nameof(RunSavedReportCommand), ct) is { } denied)
        {
            return denied;
        }

        var report = await _saved.GetAsync(request.Id, ct);
        if (report is null)
        {
            return Error.NotFound(ReportingErrorCodes.NotFound, "The saved report was not found.");
        }

        report.EnsureOwnedBy(_user.UserId!.Value);
        if (await DeniedAsync(ReportCatalog.CategoryOf(report.Definition.DataSource), nameof(RunSavedReportCommand), ct) is { } deniedSource)
        {
            return deniedSource;
        }

        return await _runner.RunAsync(report.Name, report.Definition, request.Arguments, TargetOf(request.Target), ct);
    }

    public async Task<Result<IReadOnlyList<string>>> Handle(GetReportFormatsQuery request, CancellationToken ct) =>
        await DeniedAsync(ReportCategory.Custom, nameof(GetReportFormatsQuery), ct) is { } denied ? denied : ReportViewBuilder.Formats.ToList();

    public async Task<Result<ReportViewDto>> Handle(RenderReportQuery request, CancellationToken ct)
    {
        var resolved = await ResolveAsync(request.TemplateId, request.Definition, request.Arguments, nameof(RenderReportQuery), ct);
        if (resolved.IsFailure)
        {
            return resolved.Error!;
        }

        var table = await _engine.RunAsync(resolved.Value.Definition, resolved.Value.Arguments, ct);
        return _views.Build(resolved.Value.Title, table, request.Format);
    }

    public async Task<Result<ReportDefinitionDto>> Handle(BuildReportDefinitionCommand request, CancellationToken ct)
    {
        var definition = ReportMapping.ToDomain(request.Definition);
        var category = Enum.IsDefined(definition.DataSource) ? ReportCatalog.CategoryOf(definition.DataSource) : ReportCategory.Custom;
        if (await DeniedAsync(category, nameof(BuildReportDefinitionCommand), ct) is { } denied)
        {
            return denied;
        }

        return ReportMapping.ToDto(definition.ValidateAndNormalise());
    }

    public async Task<Result<IReadOnlyList<BuilderFieldDto>>> Handle(GetReportBuilderFieldsQuery request, CancellationToken ct)
    {
        var source = Enum.Parse<ReportDataSource>(request.DataSource, true);
        if (await DeniedAsync(ReportCatalog.CategoryOf(source), nameof(GetReportBuilderFieldsQuery), ct) is { } denied)
        {
            return denied;
        }

        return ReportCatalog.FieldsOf(source).Select(f => new BuilderFieldDto(f.Name, f.Kind.ToString(), f.Aggregation.ToString())).ToList();
    }

    public async Task<Result<IReadOnlyList<AccessRuleDto>>> Handle(ListAccessRulesQuery request, CancellationToken ct) =>
        (await _rules.ListAsync(ct)).Select(ToDto).ToList();

    public async Task<Result<AccessRuleDto>> Handle(ConfigureReportAccessCommand request, CancellationToken ct)
    {
        var categories = request.Categories.Select(c => Enum.Parse<ReportCategory>(c, true)).ToList();
        var now = _clock.GetUtcNow().UtcDateTime;
        var rule = await _rules.GetByRoleAsync(request.Role, ct);
        if (rule is null)
        {
            rule = ReportAccessRule.Create(request.Role, categories, now);
            _rules.Add(rule);
        }
        else
        {
            rule.Set(request.Role, categories, now);
        }

        return ToDto(rule);
    }

    private static AccessRuleDto ToDto(ReportAccessRule r) => new(r.Role, r.Categories.Select(c => c.ToString()).ToList(), r.UpdatedAtUtc);
}
