using JobPlatform.GovernmentIntegration.Domain;
using JobPlatform.GovernmentIntegration.Domain.Interfaces.Repositories;
using Microsoft.EntityFrameworkCore;

namespace JobPlatform.GovernmentIntegration.Infrastructure.Persistence.Repositories;

internal sealed class EmployerVerificationRepository(GovernmentIntegrationDbContext db) : IEmployerVerificationRepository
{
    private static readonly VerificationState[] ActiveStates = { VerificationState.Pending, VerificationState.PendingManualReview };

    public Task<EmployerVerification?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        db.EmployerVerifications.Include(v => v.Attempts).FirstOrDefaultAsync(v => v.Id == id, ct);

    public Task<EmployerVerification?> GetActiveByEmployerAsync(Guid employerAccountId, CancellationToken ct = default) =>
        db.EmployerVerifications.Include(v => v.Attempts)
            .FirstOrDefaultAsync(v => v.EmployerAccountId == employerAccountId && ActiveStates.Contains(v.State), ct);

    public Task<bool> ExistsActiveAsync(Guid employerAccountId, CancellationToken ct = default) =>
        db.EmployerVerifications.AnyAsync(v => v.EmployerAccountId == employerAccountId && ActiveStates.Contains(v.State), ct);

    public void Add(EmployerVerification verification) => db.EmployerVerifications.Add(verification);
}
