using JobPlatform.CandidateSourcing.Domain.Interfaces.Repositories;
using JobPlatform.CandidateSourcing.Domain.Threshold;
using Microsoft.EntityFrameworkCore;

namespace JobPlatform.CandidateSourcing.Infrastructure.Persistence.Repositories;

internal sealed class QualificationThresholdRepository(CandidateSourcingDbContext db) : IQualificationThresholdRepository
{
    public Task<QualificationThreshold?> GetAsync(Guid employerAccountId, Guid jobPostingId, CancellationToken ct = default) =>
        db.QualificationThresholds.FirstOrDefaultAsync(t => t.EmployerAccountId == employerAccountId && t.JobPostingId == jobPostingId, ct);

    public void Add(QualificationThreshold threshold) => db.QualificationThresholds.Add(threshold);
}
