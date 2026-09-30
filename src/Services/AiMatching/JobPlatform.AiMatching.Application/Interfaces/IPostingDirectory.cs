using JobPlatform.AiMatching.Application.DTOs.Semantics;

namespace JobPlatform.AiMatching.Application.Interfaces;

public interface IPostingDirectory
{
    Task<PostingSource?> GetAsync(Guid jobPostingId, CancellationToken ct = default);
}
