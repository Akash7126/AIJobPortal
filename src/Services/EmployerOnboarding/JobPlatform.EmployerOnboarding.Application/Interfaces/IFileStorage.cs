namespace JobPlatform.EmployerOnboarding.Application.Interfaces;

/// <summary>Stores an uploaded file's bytes and returns its storage key; content is addressed by key, never re-derived. A local-disk adapter is the
/// default; a real deployment swaps in object storage behind this same port.</summary>
public interface IFileStorage
{
    Task<string> SaveAsync(Stream content, string suggestedFileName, string contentType, CancellationToken ct = default);

    /// <summary>Public (or short-lived signed) URL a browser can fetch the file from.</summary>
    string UrlFor(string storageKey);
}
