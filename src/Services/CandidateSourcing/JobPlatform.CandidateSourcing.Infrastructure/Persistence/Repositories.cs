using JobPlatform.CandidateSourcing.Domain;
using JobPlatform.CandidateSourcing.Domain.Insight;
using JobPlatform.CandidateSourcing.Domain.Projection;
using JobPlatform.CandidateSourcing.Domain.TalentPool;
using JobPlatform.CandidateSourcing.Domain.Threshold;
using Microsoft.EntityFrameworkCore;

namespace JobPlatform.CandidateSourcing.Infrastructure.Persistence;

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

internal sealed class QualificationThresholdRepository(CandidateSourcingDbContext db) : IQualificationThresholdRepository
{
    public Task<QualificationThreshold?> GetAsync(Guid employerAccountId, Guid jobPostingId, CancellationToken ct = default) =>
        db.QualificationThresholds.FirstOrDefaultAsync(t => t.EmployerAccountId == employerAccountId && t.JobPostingId == jobPostingId, ct);

    public void Add(QualificationThreshold threshold) => db.QualificationThresholds.Add(threshold);
}

internal sealed class CandidateInsightRepository(CandidateSourcingDbContext db) : ICandidateInsightRepository
{
    public void Add(CandidateInsight insight) => db.CandidateInsights.Add(insight);
}

internal sealed class CandidateProjectionRepository(CandidateSourcingDbContext db) : ICandidateProjectionRepository
{
    public Task<CandidateProjection?> GetAsync(Guid profileId, CancellationToken ct = default) =>
        db.CandidateProjections.FirstOrDefaultAsync(p => p.ProfileId == profileId, ct);

    public Task<CandidateProjection?> GetByOwnerAccountIdAsync(Guid ownerAccountId, CancellationToken ct = default) =>
        db.CandidateProjections.FirstOrDefaultAsync(p => p.OwnerAccountId == ownerAccountId, ct);

    public void Add(CandidateProjection projection) => db.CandidateProjections.Add(projection);
}

internal sealed class VerifiedEmployerRepository(CandidateSourcingDbContext db) : IVerifiedEmployerRepository
{
    public Task<bool> IsVerifiedAsync(Guid employerAccountId, CancellationToken ct = default) =>
        db.VerifiedEmployers.AnyAsync(e => e.EmployerAccountId == employerAccountId, ct);

    public Task<VerifiedEmployer?> GetAsync(Guid employerAccountId, CancellationToken ct = default) =>
        db.VerifiedEmployers.FirstOrDefaultAsync(e => e.EmployerAccountId == employerAccountId, ct);

    public void Add(VerifiedEmployer employer) => db.VerifiedEmployers.Add(employer);
}
