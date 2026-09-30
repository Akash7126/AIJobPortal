using System.Net.Http.Json;
using JobPlatform.JobSeekerProfile.Application.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace JobPlatform.JobSeekerProfile.Infrastructure.Adapters;

/// <summary>Local-filesystem file storage for dev/tests (FileStorage:Provider = Local, the only provider shipped in this pass -
/// see docs/bc-status/BC-04.md). Root directory FileStorage:LocalPath, default "./file-storage".</summary>
public sealed class LocalFileStorage : IFileStorage
{
    private readonly string _root;

    public LocalFileStorage(IConfiguration configuration) => _root = configuration["FileStorage:LocalPath"] is { Length: > 0 } path
        ? path
        : Path.Combine(AppContext.BaseDirectory, "file-storage");

    public async Task<string> SaveAsync(string ownerFolder, string fileName, Stream content, CancellationToken ct = default)
    {
        var storageKey = $"{ownerFolder}/{Guid.NewGuid():N}-{Sanitize(fileName)}";
        var fullPath = Path.Combine(_root, storageKey.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        await using var file = File.Create(fullPath);
        content.Position = 0;
        await content.CopyToAsync(file, ct);
        return storageKey;
    }

    public Task<Stream> OpenAsync(string storageKey, CancellationToken ct = default) =>
        Task.FromResult<Stream>(File.OpenRead(Path.Combine(_root, storageKey.Replace('/', Path.DirectorySeparatorChar))));

    public Task DeleteAsync(string storageKey, CancellationToken ct = default)
    {
        var fullPath = Path.Combine(_root, storageKey.Replace('/', Path.DirectorySeparatorChar));
        if (File.Exists(fullPath))
        {
            File.Delete(fullPath);
        }

        return Task.CompletedTask;
    }

    /// <summary>No real signing in this pass: returns a direct internal path with an expiry the caller can still honour. See known limitations.</summary>
    public Task<(string Url, DateTime ExpiresAtUtc)> GetSignedUrlAsync(string storageKey, TimeSpan validFor, CancellationToken ct = default) =>
        Task.FromResult(($"/internal/v1/file-storage/{Uri.EscapeDataString(storageKey)}", DateTime.UtcNow.Add(validFor)));

    private static string Sanitize(string fileName) => string.Concat(fileName.Where(c => !Path.GetInvalidFileNameChars().Contains(c)));
}

/// <summary>Always reports clean: no antivirus engine is wired in this pass (Q: malware scanning provider) - see docs/bc-status/BC-04.md.</summary>
public sealed class NoOpMalwareScanner : IMalwareScanner
{
    public Task<bool> IsCleanAsync(Stream content, CancellationToken ct = default) => Task.FromResult(true);
}

/// <summary>
/// Renders the share link as a simple bordered SVG frame with the URL as text - NOT a scannable QR matrix. A real QR encoder (e.g. QRCoder) is a
/// follow-up; see docs/bc-status/BC-04.md.
/// </summary>
public sealed class PlaceholderQrCodeRenderer : IQrCodeRenderer
{
    public string RenderSvg(string url)
    {
        var escaped = System.Security.SecurityElement.Escape(url);
        return $"""<svg xmlns="http://www.w3.org/2000/svg" width="200" height="200" viewBox="0 0 200 200"><rect width="200" height="200" fill="#fff" stroke="#000" stroke-width="4"/><text x="100" y="100" font-size="8" text-anchor="middle" dominant-baseline="middle">{escaped}</text></svg>""";
    }
}

/// <summary>Fake adapter for dev/tests: records the request in-memory, always succeeds.</summary>
public sealed class FakeAccountIdentityClient : IAccountIdentityClient
{
    public List<(Guid AccountId, string Reason, string Standing)> Requests { get; } = new();

    public Task<bool> RequestDeactivationAsync(Guid accountId, string reason, string standing, CancellationToken ct = default)
    {
        Requests.Add((accountId, reason, standing));
        return Task.FromResult(true);
    }
}

/// <summary>Calls BC-03's internal deactivation-request endpoint (foundation section 9.5); degrades to "accepted, will retry" on failure.</summary>
public sealed class HttpAccountIdentityClient(HttpClient http, ILogger<HttpAccountIdentityClient> logger) : IAccountIdentityClient
{
    public const string ClientName = "account-identity-internal";

    public async Task<bool> RequestDeactivationAsync(Guid accountId, string reason, string standing, CancellationToken ct = default)
    {
        try
        {
            var response = await http.PostAsJsonAsync($"/internal/v1/accounts/{accountId}/deactivation-requests", new { reason, standing }, ct);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "BC-03 deactivation request for account {AccountId} failed; the caller sees 202 and may retry.", accountId);
            return false;
        }
    }
}
