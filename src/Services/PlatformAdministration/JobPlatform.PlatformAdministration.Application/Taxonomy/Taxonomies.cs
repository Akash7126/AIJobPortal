using System.Text.RegularExpressions;
using JobPlatform.PlatformAdministration.Application.DTOs.Taxonomy;
using JobPlatform.PlatformAdministration.Application.Interfaces;
using JobPlatform.PlatformAdministration.Domain.Common;
using JobPlatform.PlatformAdministration.Domain.Taxonomy;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.PlatformAdministration.Application.Taxonomy;

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
