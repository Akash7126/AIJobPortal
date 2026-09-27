using System.Text.RegularExpressions;
using FluentValidation;
using JobPlatform.PlatformAdministration.Domain;
using JobPlatform.PlatformAdministration.Domain.Common;
using JobPlatform.PlatformAdministration.Domain.Reference;
using JobPlatform.SharedKernel.Application.Abstractions;
using JobPlatform.SharedKernel.Application.Ports;
using JobPlatform.SharedKernel.Application.Results;
using JobPlatform.SharedKernel.Common.ValueObjects;

namespace JobPlatform.PlatformAdministration.Application.Reference;

/// <param name="Op">add, edit or remove.</param>
public sealed record ReferenceChangeRequest(string Op, Guid? EntryId, string? Code, LocalizedNameView? Name, bool? IsActive);

/// <summary>US-3.1.4-07: apply entry changes to a reference file as one saved version. <paramref name="ConfirmInUse"/> allows removing referenced entries.</summary>
public sealed record UpdateReferenceFileCommand(string Type, bool ConfirmInUse, IReadOnlyList<ReferenceChangeRequest>? Changes)
    : AdminCommand<VersionResult>, ILaterSaveWinsCommand;

public static partial class ReferenceRules
{
    public const int MaxNameLength = 200;
    public const int MaxCodeLength = 50;
    public const int MaxChanges = 500;

    public static bool TryParseType(string? type, out ReferenceFileType parsed) =>
        Enum.TryParse(type, ignoreCase: true, out parsed) && Enum.IsDefined(parsed);

    public static bool TryParseOp(string? op, out ReferenceChangeKind kind) =>
        Enum.TryParse(op, ignoreCase: true, out kind) && Enum.IsDefined(kind);

    [GeneratedRegex("^[A-Za-z0-9][A-Za-z0-9._-]*$")]
    public static partial Regex CodePattern();
}

public sealed class UpdateReferenceFileValidator : AbstractValidator<UpdateReferenceFileCommand>
{
    public UpdateReferenceFileValidator()
    {
        RuleFor(c => c.Type).Must(t => ReferenceRules.TryParseType(t, out _)).WithErrorCode("VAL.Type.Invalid");
        RuleFor(c => c.Changes).NotNull().WithErrorCode("VAL.Changes.Required");
        RuleFor(c => c.Changes!).Must(list => list.Count is >= 1 and <= ReferenceRules.MaxChanges).WithErrorCode("VAL.Changes.OutOfRange").When(c => c.Changes is not null);
        RuleForEach(c => c.Changes!).SetValidator(new ChangeValidator()).When(c => c.Changes is not null);
        RuleFor(c => c.Changes!)
            .Must(list => list.Where(x => x.Code is not null).Select(x => x.Code!.Trim().ToLowerInvariant()).Distinct().Count()
                          == list.Count(x => x.Code is not null))
            .WithErrorCode("VAL.Code.Duplicate").When(c => c.Changes is not null);
    }

    private sealed class ChangeValidator : AbstractValidator<ReferenceChangeRequest>
    {
        public ChangeValidator()
        {
            RuleFor(x => x.Op).Must(op => ReferenceRules.TryParseOp(op, out _)).WithErrorCode("VAL.Op.Invalid");
            When(x => ReferenceRules.TryParseOp(x.Op, out var k) && k == ReferenceChangeKind.Add, () =>
            {
                RuleFor(x => x.Code).NotEmpty().WithErrorCode("VAL.Code.Required");
                RuleFor(x => x.Code!).MaximumLength(ReferenceRules.MaxCodeLength).WithErrorCode("VAL.Code.TooLong")
                    .Matches(ReferenceRules.CodePattern()).WithErrorCode("VAL.Code.InvalidFormat").When(x => !string.IsNullOrEmpty(x.Code));
                RuleFor(x => x.Name).NotNull().WithErrorCode("VAL.Name.Required");
            });
            When(x => ReferenceRules.TryParseOp(x.Op, out var k) && k != ReferenceChangeKind.Add, () =>
                RuleFor(x => x.EntryId).NotNull().WithErrorCode("VAL.EntryId.Required"));
            When(x => ReferenceRules.TryParseOp(x.Op, out var k) && k == ReferenceChangeKind.Edit, () =>
                RuleFor(x => x).Must(x => x.Name is not null || x.IsActive is not null).WithErrorCode("VAL.Change.Empty"));
            When(x => x.Name is not null, () =>
            {
                RuleFor(x => x.Name!.Ar).NotEmpty().WithErrorCode("VAL.Name.Ar.Invalid").MaximumLength(ReferenceRules.MaxNameLength).WithErrorCode("VAL.Name.Ar.Invalid");
                RuleFor(x => x.Name!.En).NotEmpty().WithErrorCode("VAL.Name.En.Invalid").MaximumLength(ReferenceRules.MaxNameLength).WithErrorCode("VAL.Name.En.Invalid");
            });
        }
    }
}

internal sealed class UpdateReferenceFileHandler : ICommandHandler<UpdateReferenceFileCommand, VersionResult>
{
    private readonly IReferenceFileRepository _files;
    private readonly IReferenceUsageChecker _usage;
    private readonly ICurrentUser _user;
    private readonly TimeProvider _clock;

    public UpdateReferenceFileHandler(IReferenceFileRepository files, IReferenceUsageChecker usage, ICurrentUser user, TimeProvider clock)
    {
        _files = files;
        _usage = usage;
        _user = user;
        _clock = clock;
    }

    public async Task<Result<VersionResult>> Handle(UpdateReferenceFileCommand request, CancellationToken ct)
    {
        ReferenceRules.TryParseType(request.Type, out var type);
        var file = await _files.GetByTypeAsync(type, ct);
        if (file is null)
        {
            return Error.NotFound(ErrorCodes.NotFound, "The reference file was not found.");
        }

        var changes = request.Changes!.Select(Map).ToList();
        IReadOnlyCollection<string> inUse = Array.Empty<string>();
        var candidates = file.RemovalCandidateCodes(changes);
        if (candidates.Count > 0 && !request.ConfirmInUse)
        {
            var usage = await _usage.CheckAsync(type.ToString().ToLowerInvariant(), candidates, ct);
            // Fail-safe (handover 6.2): when the owners cannot answer, every removal needs explicit confirmation.
            inUse = usage.Available ? usage.InUse : candidates;
        }

        file.ApplyChanges(changes, request.ConfirmInUse, inUse, ActorFactory.From(_user), _clock.GetUtcNow().UtcDateTime);
        return new VersionResult(file.FileVersion);
    }

    private static ReferenceChange Map(ReferenceChangeRequest r)
    {
        ReferenceRules.TryParseOp(r.Op, out var kind);
        return new ReferenceChange(kind, r.EntryId, r.Code, r.Name is null ? null : new LocalizedText(r.Name.Ar.Trim(), r.Name.En.Trim()), r.IsActive);
    }
}

public sealed record GetReferenceFileQuery(string Type) : AdminQuery<ReferenceFileView>;

/// <summary>Consumers' read (GET /internal/v1/reference-files/{type}), Redis-cached for 30 minutes.</summary>
public sealed record GetReferenceFileForConsumersQuery(string Type) : ServiceQuery<ReferenceFileView>;

internal sealed class ReferenceFileReader
{
    private readonly IAdminReadStore _store;
    private readonly IReferenceDataCache _cache;

    public ReferenceFileReader(IAdminReadStore store, IReferenceDataCache cache)
    {
        _store = store;
        _cache = cache;
    }

    public async Task<Result<ReferenceFileView>> ReadAsync(string type, bool useCache, CancellationToken ct)
    {
        if (!ReferenceRules.TryParseType(type, out var parsed))
        {
            return Error.NotFound(ErrorCodes.NotFound, "The reference file was not found.");
        }

        var canonical = parsed.ToString().ToLowerInvariant();
        var key = CacheKeys.Reference(canonical);
        if (useCache && await _cache.GetAsync<ReferenceFileView>(key, ct) is { } cached)
        {
            return cached;
        }

        if (await _store.GetReferenceFileAsync(canonical, ct) is not { } view)
        {
            return Error.NotFound(ErrorCodes.NotFound, "The reference file was not found.");
        }

        if (useCache)
        {
            await _cache.SetAsync(key, view, CacheKeys.ReferenceTtl, ct);
        }

        return view;
    }
}

internal sealed class GetReferenceFileHandler : IQueryHandler<GetReferenceFileQuery, ReferenceFileView>
{
    private readonly ReferenceFileReader _reader;

    public GetReferenceFileHandler(ReferenceFileReader reader) => _reader = reader;

    public Task<Result<ReferenceFileView>> Handle(GetReferenceFileQuery request, CancellationToken ct) => _reader.ReadAsync(request.Type, false, ct);
}

internal sealed class GetReferenceFileForConsumersHandler : IQueryHandler<GetReferenceFileForConsumersQuery, ReferenceFileView>
{
    private readonly ReferenceFileReader _reader;

    public GetReferenceFileForConsumersHandler(ReferenceFileReader reader) => _reader = reader;

    public Task<Result<ReferenceFileView>> Handle(GetReferenceFileForConsumersQuery request, CancellationToken ct) => _reader.ReadAsync(request.Type, true, ct);
}
