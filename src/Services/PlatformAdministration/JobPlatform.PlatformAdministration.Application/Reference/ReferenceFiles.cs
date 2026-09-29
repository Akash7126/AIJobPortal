using System.Text.RegularExpressions;
using JobPlatform.PlatformAdministration.Application.DTOs.Reference;
using JobPlatform.PlatformAdministration.Domain.Common;
using JobPlatform.PlatformAdministration.Domain.Reference;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.PlatformAdministration.Application.Reference;

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
