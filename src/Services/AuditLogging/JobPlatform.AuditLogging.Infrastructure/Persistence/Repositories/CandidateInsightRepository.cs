using JobPlatform.AuditLogging.Domain;
using JobPlatform.AuditLogging.Domain.Interfaces.Repositories;
using Microsoft.EntityFrameworkCore;

namespace JobPlatform.AuditLogging.Infrastructure.Persistence.Repositories;

internal sealed class CandidateInsightRepository : ICandidateInsightRepository
{
    private readonly AuditDbContext _db;

    public CandidateInsightRepository(AuditDbContext db) => _db = db;

    public Task<CandidateInsightRecord?> GetAsync(Guid insightId, CancellationToken ct = default) =>
        _db.CandidateInsights.FirstOrDefaultAsync(i => i.Id == insightId, ct);

    public void Add(CandidateInsightRecord insight) => _db.CandidateInsights.Add(insight);
}
