using JobPlatform.EmployerOnboarding.Application;
using Microsoft.Extensions.Configuration;

namespace JobPlatform.EmployerOnboarding.Infrastructure.Adapters;

/// <summary>Default IFileStorage adapter: local disk under FileStorage:RootPath (or a temp directory in dev). A real deployment swaps this for
/// object storage behind the same port - the Application layer never knows which.</summary>
internal sealed class LocalFileStorage : IFileStorage
{
    private readonly string _root;
    private readonly string _publicBaseUrl;

    public LocalFileStorage(IConfiguration configuration)
    {
        _root = configuration["FileStorage:RootPath"] is { Length: > 0 } configured
            ? configured
            : Path.Combine(Path.GetTempPath(), "jobplatform-employer-onboarding");
        Directory.CreateDirectory(_root);
        _publicBaseUrl = configuration["FileStorage:PublicBaseUrl"]?.TrimEnd('/') ?? "/files/employer-onboarding";
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

/// <summary>Default IMalwareScanner adapter: no scanner is wired in this environment, so every file passes. A real deployment swaps this for a
/// ClamAV/cloud adapter behind the same port; the domain and application layers do not change.</summary>
internal sealed class PassthroughMalwareScanner : IMalwareScanner
{
    public Task<ScanResult> ScanAsync(Stream content, string contentType, CancellationToken ct = default) => Task.FromResult(new ScanResult(true, null));
}
