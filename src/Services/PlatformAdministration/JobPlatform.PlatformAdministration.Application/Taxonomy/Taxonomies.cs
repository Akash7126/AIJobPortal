using System.Text.RegularExpressions;
using FluentValidation;
using JobPlatform.PlatformAdministration.Domain;
using JobPlatform.PlatformAdministration.Domain.Common;
using JobPlatform.PlatformAdministration.Domain.Taxonomy;
using JobPlatform.SharedKernel.Application.Abstractions;
using JobPlatform.SharedKernel.Application.Ports;
using JobPlatform.SharedKernel.Application.Results;
using JobPlatform.SharedKernel.Common.ValueObjects;

namespace JobPlatform.PlatformAdministration.Application.Taxonomy;

/// <param name="Op">add, edit or remove (soft-remove).</param>
public sealed record TaxonomyChangeRequest(string Op, string Code, LocalizedNameView? Name, string? ParentCode, IReadOnlyList<string>? Synonyms, bool? IsActive);

/// <summary>US-3.1.4-08: apply node changes as one saved version. The taxonomy of an unknown (but valid) type is created on first use (open set, GAP-003).</summary>
public sealed record UpdatePlatformTaxonomyCommand(string Type, IReadOnlyList<TaxonomyChangeRequest>? Changes) : AdminCommand<VersionResult>, ILaterSaveWinsCommand;

public static partial class TaxonomyRules
{
    public const int MaxChanges = 500;
    public const int MaxNameLength = 200;
    public const int MaxCodeLength = 100;
    public const int MaxSynonyms = 10;
    public const int MaxSynonymLength = 100;

    public static bool TryParseOp(string? op, out TaxonomyChangeKind kind) => Enum.TryParse(op, ignoreCase: true, out kind) && Enum.IsDefined(kind);

    [GeneratedRegex("^[A-Za-z0-9][A-Za-z0-9._-]*$")]
    public static partial Regex CodePattern();
}

public sealed class UpdatePlatformTaxonomyValidator : AbstractValidator<UpdatePlatformTaxonomyCommand>
{
    public UpdatePlatformTaxonomyValidator()
    {
        RuleFor(c => c.Type).Must(t => TaxonomyTypes.IsValid(TaxonomyTypes.Normalise(t))).WithErrorCode("VAL.Type.Invalid");
        RuleFor(c => c.Changes).NotNull().WithErrorCode("VAL.Changes.Required");
        RuleFor(c => c.Changes!).Must(list => list.Count is >= 1 and <= TaxonomyRules.MaxChanges).WithErrorCode("VAL.Changes.OutOfRange").When(c => c.Changes is not null);
        RuleForEach(c => c.Changes!).SetValidator(new ChangeValidator()).When(c => c.Changes is not null);
        RuleFor(c => c.Changes!)
            .Must(list => list.Select(x => (x.Code ?? string.Empty).Trim().ToLowerInvariant()).Distinct().Count() == list.Count)
            .WithErrorCode("VAL.Code.Duplicate").When(c => c.Changes is not null);
    }

    private sealed class ChangeValidator : AbstractValidator<TaxonomyChangeRequest>
    {
        public ChangeValidator()
        {
            RuleFor(x => x.Op).Must(op => TaxonomyRules.TryParseOp(op, out _)).WithErrorCode("VAL.Op.Invalid");
            RuleFor(x => x.Code).NotEmpty().WithErrorCode("VAL.Code.Required");
            RuleFor(x => x.Code).MaximumLength(TaxonomyRules.MaxCodeLength).WithErrorCode("VAL.Code.TooLong")
                .Matches(TaxonomyRules.CodePattern()).WithErrorCode("VAL.Code.InvalidFormat").When(x => !string.IsNullOrEmpty(x.Code));
            When(x => TaxonomyRules.TryParseOp(x.Op, out var k) && k == TaxonomyChangeKind.Add,
                () => RuleFor(x => x.Name).NotNull().WithErrorCode("VAL.Name.Required"));
            When(x => x.Name is not null, () =>
            {
                RuleFor(x => x.Name!.Ar).NotEmpty().WithErrorCode("VAL.Name.Ar.Invalid").MaximumLength(TaxonomyRules.MaxNameLength).WithErrorCode("VAL.Name.Ar.Invalid");
                RuleFor(x => x.Name!.En).NotEmpty().WithErrorCode("VAL.Name.En.Invalid").MaximumLength(TaxonomyRules.MaxNameLength).WithErrorCode("VAL.Name.En.Invalid");
            });
            RuleFor(x => x).Must(x => !string.Equals(x.ParentCode?.Trim(), x.Code?.Trim(), StringComparison.OrdinalIgnoreCase))
                .WithErrorCode("VAL.ParentCode.Self").When(x => x.ParentCode is not null);
            RuleFor(x => x.Synonyms!).Must(s => s.Count <= TaxonomyRules.MaxSynonyms && s.All(v => !string.IsNullOrWhiteSpace(v) && v.Length <= TaxonomyRules.MaxSynonymLength))
                .WithErrorCode("VAL.Synonyms.Invalid").When(x => x.Synonyms is not null);
        }
    }
}

internal sealed class UpdatePlatformTaxonomyHandler : ICommandHandler<UpdatePlatformTaxonomyCommand, VersionResult>
{
    private readonly IPlatformTaxonomyRepository _taxonomies;
    private readonly ICurrentUser _user;
    private readonly TimeProvider _clock;

    public UpdatePlatformTaxonomyHandler(IPlatformTaxonomyRepository taxonomies, ICurrentUser user, TimeProvider clock)
    {
        _taxonomies = taxonomies;
        _user = user;
        _clock = clock;
    }

    public async Task<Result<VersionResult>> Handle(UpdatePlatformTaxonomyCommand request, CancellationToken ct)
    {
        var now = _clock.GetUtcNow().UtcDateTime;
        var type = TaxonomyTypes.Normalise(request.Type);
        var taxonomy = await _taxonomies.GetByTypeAsync(type, ct);
        if (taxonomy is null)
        {
            taxonomy = PlatformTaxonomy.Create(type, now);
            _taxonomies.Add(taxonomy);
            _taxonomies.AddSnapshot(taxonomy.CreateSnapshot(now));
        }

        var changes = request.Changes!.Select(c =>
        {
            TaxonomyRules.TryParseOp(c.Op, out var kind);
            return new TaxonomyChange(kind, c.Code, c.Name is null ? null : new LocalizedText(c.Name.Ar.Trim(), c.Name.En.Trim()), c.ParentCode,
                c.Synonyms?.Select(s => s.Trim()).ToArray(), c.IsActive);
        }).ToList();

        taxonomy.ApplyChanges(changes, ActorFactory.From(_user), now);
        _taxonomies.AddSnapshot(taxonomy.CreateSnapshot(now));
        return new VersionResult(taxonomy.TaxonomyVersion);
    }
}

public sealed record GetPlatformTaxonomyQuery(string Type, int? Version = null) : AdminQuery<TaxonomyView>;

/// <summary>Consumers' read (GET /internal/v1/taxonomies/{type}?version=): the current version pointer (5 min) and immutable per-version content (60 min) are cached.</summary>
public sealed record GetTaxonomyForConsumersQuery(string Type, int? Version = null) : ServiceQuery<TaxonomyView>;

internal sealed class TaxonomyReader
{
    private readonly IAdminReadStore _store;
    private readonly IReferenceDataCache _cache;

    public TaxonomyReader(IAdminReadStore store, IReferenceDataCache cache)
    {
        _store = store;
        _cache = cache;
    }

    public async Task<Result<TaxonomyView>> ReadAsync(string type, int? version, bool useCache, CancellationToken ct)
    {
        var canonical = TaxonomyTypes.Normalise(type);
        var notFound = Error.NotFound(ErrorCodes.NotFound, "The taxonomy or version was not found.");
        if (!TaxonomyTypes.IsValid(canonical))
        {
            return notFound;
        }

        var effective = version;
        if (effective is null)
        {
            var pointerKey = CacheKeys.TaxonomyCurrent(canonical);
            effective = useCache ? await _cache.GetAsync<int?>(pointerKey, ct) : null;
            if (effective is null)
            {
                effective = await _store.GetTaxonomyCurrentVersionAsync(canonical, ct);
                if (effective is null)
                {
                    return notFound;
                }

                if (useCache)
                {
                    await _cache.SetAsync<int?>(pointerKey, effective, CacheKeys.TaxonomyCurrentTtl, ct);
                }
            }
        }

        var key = CacheKeys.Taxonomy(canonical, effective.Value);
        if (useCache && await _cache.GetAsync<TaxonomyView>(key, ct) is { } cached)
        {
            return cached;
        }

        if (await _store.GetTaxonomyAsync(canonical, effective, ct) is not { } view)
        {
            return notFound;
        }

        if (useCache)
        {
            await _cache.SetAsync(key, view, CacheKeys.TaxonomyVersionTtl, ct);
        }

        return view;
    }
}

internal sealed class GetPlatformTaxonomyHandler : IQueryHandler<GetPlatformTaxonomyQuery, TaxonomyView>
{
    private readonly TaxonomyReader _reader;

    public GetPlatformTaxonomyHandler(TaxonomyReader reader) => _reader = reader;

    public Task<Result<TaxonomyView>> Handle(GetPlatformTaxonomyQuery request, CancellationToken ct) => _reader.ReadAsync(request.Type, request.Version, false, ct);
}

internal sealed class GetTaxonomyForConsumersHandler : IQueryHandler<GetTaxonomyForConsumersQuery, TaxonomyView>
{
    private readonly TaxonomyReader _reader;

    public GetTaxonomyForConsumersHandler(TaxonomyReader reader) => _reader = reader;

    public Task<Result<TaxonomyView>> Handle(GetTaxonomyForConsumersQuery request, CancellationToken ct) => _reader.ReadAsync(request.Type, request.Version, true, ct);
}
