using JobPlatform.CandidateSourcing.Domain.Interfaces.Repositories;
using JobPlatform.CandidateSourcing.Domain.Projection;
using Microsoft.EntityFrameworkCore;

namespace JobPlatform.CandidateSourcing.Infrastructure.Persistence.Repositories;

internal sealed class VerifiedEmployerRepository(CandidateSourcingDbContext db) : IVerifiedEmployerRepository
{
    public Task<bool> IsVerifiedAsync(Guid employerAccountId, CancellationToken ct = default) =>
        db.VerifiedEmployers.AnyAsync(e => e.EmployerAccountId == employerAccountId, ct);

    public Task<VerifiedEmployer?> GetAsync(Guid employerAccountId, CancellationToken ct = default) =>
        db.VerifiedEmployers.FirstOrDefaultAsync(e => e.EmployerAccountId == employerAccountId, ct);

    public void Add(VerifiedEmployer employer) => db.VerifiedEmployers.Add(employer);
}
