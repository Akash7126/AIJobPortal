namespace JobPlatform.HelpContent.Application.Interfaces;

/// <summary>Stores an uploaded media file's bytes and returns its storage key; content is addressed by key, never re-derived.</summary>
public interface IMediaStorage
{
    Task<string> SaveAsync(Stream content, string suggestedFileName, string contentType, CancellationToken ct = default);

    string UrlFor(string storageKey);
}
