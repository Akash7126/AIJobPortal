using FluentValidation;
using JobPlatform.Reporting.Domain;
using JobPlatform.SharedKernel.Application.Abstractions;
using JobPlatform.SharedKernel.Application.Ports;
using JobPlatform.SharedKernel.Application.Results;
using Unit = JobPlatform.SharedKernel.Application.Results.Unit;

namespace JobPlatform.Reporting.Application;

// US-3.5.4-02 templates and US-3.5.4-07 saved report library.

public sealed record ListTemplatesQuery : CustomRequest, IQuery<IReadOnlyList<TemplateDto>>;

public sealed record GetTemplateQuery(Guid Id) : CustomRequest, IQuery<TemplateDto>;

/// <summary>Creates a template (Id null) or replaces it; the later save wins (AC-04).</summary>
public sealed record SaveReportTemplateCommand(Guid? Id, string Name, string DataSource, IReadOnlyList<TemplateParameterDto>? Parameters)
    : CustomCommandRequest, ICommand<TemplateDto>;

public sealed record SaveReportCommand(string Name, ReportDefinitionDto Definition) : CustomCommandRequest, ICommand<SavedReportDto>;

public sealed record ListSavedReportsQuery : CustomRequest, IQuery<IReadOnlyList<SavedReportDto>>;

public sealed record DeleteSavedReportCommand(Guid Id) : CustomCommandRequest, ICommand;

/// <summary>Scheduled: archives saved reports past their 12-month retention (AC-03). Returns the number archived.</summary>
public sealed record ArchiveExpiredSavedReportsCommand(int BatchSize) : ICommand<int>;

/// <summary>SaveReportTemplateValidator (handover 7): unique parameter names, known types (min/max/default consistency is the domain's INV-03).</summary>
public sealed class SaveReportTemplateValidator : AbstractValidator<SaveReportTemplateCommand>
{
    public SaveReportTemplateValidator()
    {
        RuleFor(x => x.Name).NotEmpty().WithErrorCode("VAL.Name.Required").MaximumLength(150).WithErrorCode("VAL.Name.TooLong");
        RuleFor(x => x.DataSource).Must(s => Enum.TryParse<ReportDataSource>(s, true, out _)).WithErrorCode("VAL.DataSource.Unknown");
        RuleFor(x => x.Parameters).Must(p => p is null || p.Count <= 30).WithErrorCode("VAL.Parameters.TooMany");
        RuleFor(x => x.Parameters).Must(p => p is null || p.Select(x => x.Name?.ToLowerInvariant()).Distinct().Count() == p.Count).WithErrorCode("VAL.Parameters.DuplicateName");
        RuleForEach(x => x.Parameters).ChildRules(p =>
        {
            p.RuleFor(x => x.Name).NotEmpty().WithErrorCode("VAL.Parameter.Name.Required");
            p.RuleFor(x => x.Type).Must(t => Enum.TryParse<ParameterType>(t, true, out _)).WithErrorCode("VAL.Parameter.Type.Unknown");
        });
    }
}

public sealed class SaveReportValidator : AbstractValidator<SaveReportCommand>
{
    public SaveReportValidator()
    {
        RuleFor(x => x.Name).NotEmpty().WithErrorCode("VAL.Name.Required").MaximumLength(150).WithErrorCode("VAL.Name.TooLong");
        RuleFor(x => x.Definition).NotNull().WithErrorCode("VAL.Definition.Required");
    }
}

internal sealed class ReportLibraryHandlers :
    IQueryHandler<ListTemplatesQuery, IReadOnlyList<TemplateDto>>,
    IQueryHandler<GetTemplateQuery, TemplateDto>,
    ICommandHandler<SaveReportTemplateCommand, TemplateDto>,
    ICommandHandler<SaveReportCommand, SavedReportDto>,
    IQueryHandler<ListSavedReportsQuery, IReadOnlyList<SavedReportDto>>,
    ICommandHandler<DeleteSavedReportCommand, Unit>,
    ICommandHandler<ArchiveExpiredSavedReportsCommand, int>
{
    private readonly IReportAccessGuard _guard;
    private readonly IReportTemplateRepository _templates;
    private readonly ISavedReportRepository _saved;
    private readonly ICurrentUser _user;
    private readonly TimeProvider _clock;

    public ReportLibraryHandlers(IReportAccessGuard guard, IReportTemplateRepository templates, ISavedReportRepository saved, ICurrentUser user, TimeProvider clock)
    {
        _guard = guard;
        _templates = templates;
        _saved = saved;
        _user = user;
        _clock = clock;
    }

    private DateTime Now => _clock.GetUtcNow().UtcDateTime;

    private async Task<Error?> DeniedAsync(string name, CancellationToken ct) => (await _guard.EnsureAsync(ReportCategory.Custom, name, ct)).Error;

    public async Task<Result<IReadOnlyList<TemplateDto>>> Handle(ListTemplatesQuery request, CancellationToken ct) =>
        await DeniedAsync(nameof(ListTemplatesQuery), ct) is { } denied ? denied : (await _templates.ListAsync(ct)).Select(ReportMapping.ToDto).ToList();

    public async Task<Result<TemplateDto>> Handle(GetTemplateQuery request, CancellationToken ct)
    {
        if (await DeniedAsync(nameof(GetTemplateQuery), ct) is { } denied)
        {
            return denied;
        }

        var template = await _templates.GetAsync(request.Id, ct);
        return template is null ? Error.NotFound(ReportingErrorCodes.NotFound, "The template was not found.") : ReportMapping.ToDto(template);
    }

    public async Task<Result<TemplateDto>> Handle(SaveReportTemplateCommand request, CancellationToken ct)
    {
        if (await DeniedAsync(nameof(SaveReportTemplateCommand), ct) is { } denied)
        {
            return denied;
        }

        var source = Enum.Parse<ReportDataSource>(request.DataSource, true);
        var parameters = (request.Parameters ?? Array.Empty<TemplateParameterDto>()).Select(ReportMapping.ToDomain).ToList();
        ReportTemplate template;
        if (request.Id is { } id)
        {
            var existing = await _templates.GetAsync(id, ct);
            if (existing is null)
            {
                return Error.NotFound(ReportingErrorCodes.NotFound, "The template was not found.");
            }

            existing.Save(request.Name, source, parameters, Now);
            template = existing;
        }
        else
        {
            template = ReportTemplate.Create(request.Name, source, parameters, _user.UserId!.Value, Now);
            _templates.Add(template);
        }

        return ReportMapping.ToDto(template);
    }

    public async Task<Result<SavedReportDto>> Handle(SaveReportCommand request, CancellationToken ct)
    {
        if (await DeniedAsync(nameof(SaveReportCommand), ct) is { } denied)
        {
            return denied;
        }

        var report = SavedReport.Save(_user.UserId!.Value, request.Name, ReportMapping.ToDomain(request.Definition), Now);
        _saved.Add(report);
        return ReportMapping.ToDto(report);
    }

    /// <summary>Default visibility is the caller's own reports (A-02-015).</summary>
    public async Task<Result<IReadOnlyList<SavedReportDto>>> Handle(ListSavedReportsQuery request, CancellationToken ct) =>
        await DeniedAsync(nameof(ListSavedReportsQuery), ct) is { } denied
            ? denied
            : (await _saved.ListOwnedAsync(_user.UserId!.Value, ct)).Select(ReportMapping.ToDto).ToList();

    public async Task<Result<Unit>> Handle(DeleteSavedReportCommand request, CancellationToken ct)
    {
        if (await DeniedAsync(nameof(DeleteSavedReportCommand), ct) is { } denied)
        {
            return denied;
        }

        var report = await _saved.GetAsync(request.Id, ct);
        if (report is null)
        {
            return Error.NotFound(ReportingErrorCodes.NotFound, "The saved report was not found.");
        }

        report.EnsureOwnedBy(_user.UserId!.Value);
        _saved.Remove(report);
        return Result.Success();
    }

    public async Task<Result<int>> Handle(ArchiveExpiredSavedReportsCommand request, CancellationToken ct)
    {
        var expired = await _saved.ListExpiredAsync(Now, request.BatchSize, ct);
        foreach (var report in expired)
        {
            report.Archive(Now);
        }

        return expired.Count;
    }
}
