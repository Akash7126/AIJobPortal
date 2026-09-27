using JobPlatform.HelpContent.Application;
using Microsoft.Extensions.Configuration;

namespace JobPlatform.HelpContent.Infrastructure.Adapters;

/// <summary>Default IMediaStorage adapter: local disk under FileStorage:RootPath (or a temp directory in dev). A real deployment swaps this
/// for object storage / an external streaming host (handover Q-01) behind the same port.</summary>
internal sealed class LocalMediaStorage : IMediaStorage
{
    private readonly string _root;
    private readonly string _publicBaseUrl;

    public LocalMediaStorage(IConfiguration configuration)
    {
        _root = configuration["FileStorage:RootPath"] is { Length: > 0 } configured
            ? configured
            : Path.Combine(Path.GetTempPath(), "jobplatform-help-content");
        Directory.CreateDirectory(_root);
        _publicBaseUrl = configuration["FileStorage:PublicBaseUrl"]?.TrimEnd('/') ?? "/files/help-content";
    }

    public async Task<string> SaveAsync(Stream content, string suggestedFileName, string contentType, CancellationToken ct = default)
    {
        var extension = Path.GetExtension(suggestedFileName);
        var storageKey = $"{Guid.NewGuid():N}{extension}";
        await using var file = File.Create(Path.Combine(_root, storageKey));
        await content.CopyToAsync(file, ct);
        return storageKey;
    }

    public string UrlFor(string storageKey) => $"{_publicBaseUrl}/{storageKey}";
}
