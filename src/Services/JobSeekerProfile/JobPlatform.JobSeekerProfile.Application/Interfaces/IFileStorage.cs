namespace JobPlatform.JobSeekerProfile.Application.Interfaces;

/// <summary>Anti-corruption port to durable file storage (resumes/documents). Adapter chosen by FileStorage:Provider.</summary>
public interface IFileStorage
{
    Task<string> SaveAsync(string ownerFolder, string fileName, Stream content, CancellationToken ct = default);
    Task<Stream> OpenAsync(string storageKey, CancellationToken ct = default);
    Task DeleteAsync(string storageKey, CancellationToken ct = default);

    /// <summary>Short-lived signed URL another service (BC-10) can fetch the content from.</summary>
    Task<(string Url, DateTime ExpiresAtUtc)> GetSignedUrlAsync(string storageKey, TimeSpan validFor, CancellationToken ct = default);
}
