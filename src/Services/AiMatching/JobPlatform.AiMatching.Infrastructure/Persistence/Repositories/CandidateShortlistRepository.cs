using JobPlatform.AiMatching.Domain;
using JobPlatform.AiMatching.Domain.Interfaces.Repositories;
using Microsoft.EntityFrameworkCore;

namespace JobPlatform.AiMatching.Infrastructure.Persistence.Repositories;

internal sealed class CandidateShortlistRepository(AiMatchingDbContext db) : ICandidateShortlistRepository
{
    public Task<CandidateShortlist?> GetAsync(Guid id, CancellationToken ct = default) => db.CandidateShortlists.FirstOrDefaultAsync(s => s.Id == id, ct);

    public void Add(CandidateShortlist shortlist) => db.CandidateShortlists.Add(shortlist);
}
