namespace JobPlatform.AiMatching.Domain.Interfaces.Repositories;

public interface IResumeParsedDataRepository
{
    Task<ResumeParsedData?> GetAsync(Guid id, CancellationToken ct = default);

    /// <summary>The current (not superseded) parse of a resume, if any.</summary>
    Task<ResumeParsedData?> GetCurrentByResumeAsync(Guid resumeId, CancellationToken ct = default);

    /// <summary>Latest not-superseded parse of a profile (any resume).</summary>
    Task<ResumeParsedData?> GetLatestByProfileAsync(Guid profileId, CancellationToken ct = default);

    Task<bool> ExistsAsync(Guid resumeId, string sha256, CancellationToken ct = default);

    void Add(ResumeParsedData data);
}
