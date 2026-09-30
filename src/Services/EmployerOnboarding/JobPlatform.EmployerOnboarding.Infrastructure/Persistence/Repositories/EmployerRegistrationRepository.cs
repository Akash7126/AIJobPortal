using JobPlatform.EmployerOnboarding.Domain;
using JobPlatform.EmployerOnboarding.Domain.Interfaces.Repositories;
using Microsoft.EntityFrameworkCore;

namespace JobPlatform.EmployerOnboarding.Infrastructure.Persistence.Repositories;

internal sealed class EmployerRegistrationRepository(EmployerOnboardingDbContext db) : IEmployerRegistrationRepository
{
    public Task<EmployerRegistration?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        db.EmployerRegistrations.FirstOrDefaultAsync(r => r.Id == id, ct);

    public Task<EmployerRegistration?> GetByEmployerAsync(Guid employerAccountId, CancellationToken ct = default) =>
        db.EmployerRegistrations.FirstOrDefaultAsync(r => r.EmployerAccountId == employerAccountId, ct);

    public void Add(EmployerRegistration registration) => db.EmployerRegistrations.Add(registration);
}
