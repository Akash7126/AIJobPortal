using JobPlatform.CandidateSourcing.Domain.Interfaces.Repositories;
using JobPlatform.CandidateSourcing.Domain.TalentPool;
using Microsoft.EntityFrameworkCore;

namespace JobPlatform.CandidateSourcing.Infrastructure.Persistence.Repositories;

/// <summary>Aggregate-oriented repositories (foundation section 8): no IQueryable leaks, no SaveChanges (the unit of work commits).</summary>
internal sealed class TalentPoolEntryRepository(CandidateSourcingDbContext db) : ITalentPoolEntryRepository
{
    public Task<TalentPoolEntry?> GetAsync(Guid employerAccountId, Guid candidateProfileId, Guid jobPostingId, CancellationToken ct = default) =>
        db.TalentPoolEntries.FirstOrDefaultAsync(e =>
            e.EmployerAccountId == employerAccountId && e.CandidateProfileId == candidateProfileId && e.JobPostingId == jobPostingId && !e.Removed, ct);

    public Task<TalentPoolEntry?> GetByIdAsync(Guid id, CancellationToken ct = default) => db.TalentPoolEntries.FirstOrDefaultAsync(e => e.Id == id, ct);

    public async Task<IReadOnlyList<TalentPoolEntry>> ListByEmployerAsync(Guid employerAccountId, CancellationToken ct = default) =>
        await db.TalentPoolEntries.Where(e => e.EmployerAccountId == employerAccountId && !e.Removed).ToListAsync(ct);

    public void Add(TalentPoolEntry entry) => db.TalentPoolEntries.Add(entry);
}
