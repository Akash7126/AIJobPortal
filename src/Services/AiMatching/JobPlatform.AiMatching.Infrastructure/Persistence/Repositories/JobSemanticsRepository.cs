using JobPlatform.AiMatching.Domain;
using JobPlatform.AiMatching.Domain.Interfaces.Repositories;
using Microsoft.EntityFrameworkCore;

namespace JobPlatform.AiMatching.Infrastructure.Persistence.Repositories;

internal sealed class JobSemanticsRepository(AiMatchingDbContext db) : IJobSemanticsRepository
{
    public Task<JobSemantics?> GetAsync(Guid jobPostingId, CancellationToken ct = default) => db.JobSemantics.FirstOrDefaultAsync(s => s.Id == jobPostingId, ct);

    public void Add(JobSemantics semantics) => db.JobSemantics.Add(semantics);
}
