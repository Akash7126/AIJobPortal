namespace JobPlatform.AiMatching.Application.Interfaces;

/// <summary>Fetches the resume file through BC-04's short-lived signed URL. Null = the resume is unknown.</summary>
public interface IResumeContentSource
{
    Task<ResumeContent?> FetchAsync(Guid resumeId, CancellationToken ct = default);
}
