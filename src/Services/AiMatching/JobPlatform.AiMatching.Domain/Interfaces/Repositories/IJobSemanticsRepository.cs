namespace JobPlatform.AiMatching.Domain.Interfaces.Repositories;

public interface IJobSemanticsRepository
{
    Task<JobSemantics?> GetAsync(Guid jobPostingId, CancellationToken ct = default);

    void Add(JobSemantics semantics);
}
