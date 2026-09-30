using JobPlatform.AiMatching.Domain;
using JobPlatform.AiMatching.Domain.Interfaces.Repositories;
using Microsoft.EntityFrameworkCore;

namespace JobPlatform.AiMatching.Infrastructure.Persistence.Repositories;

internal sealed class MatchScoreRepository(AiMatchingDbContext db) : IMatchScoreRepository
{
    public Task<MatchScore?> GetPairAsync(Guid profileId, Guid jobPostingId, CancellationToken ct = default) =>
        db.MatchScores.FirstOrDefaultAsync(s => s.ProfileId == profileId && s.JobPostingId == jobPostingId, ct);

    public async Task<IReadOnlyList<MatchScore>> ListByPostingAsync(Guid jobPostingId, CancellationToken ct = default) =>
        await db.MatchScores.Where(s => s.JobPostingId == jobPostingId).OrderByDescending(s => s.Score).ThenBy(s => s.ProfileId).ToListAsync(ct);

    public async Task<IReadOnlyList<MatchScore>> ListByProfileAsync(Guid profileId, CancellationToken ct = default) =>
        await db.MatchScores.Where(s => s.ProfileId == profileId).OrderByDescending(s => s.Score).ThenBy(s => s.JobPostingId).ToListAsync(ct);

    public async Task<int> RemoveByPostingAsync(Guid jobPostingId, CancellationToken ct = default)
    {
        var stored = await db.MatchScores.Where(s => s.JobPostingId == jobPostingId).ToListAsync(ct);
        db.MatchScores.RemoveRange(stored);
        return stored.Count;
    }

    public void Add(MatchScore score) => db.MatchScores.Add(score);
}
