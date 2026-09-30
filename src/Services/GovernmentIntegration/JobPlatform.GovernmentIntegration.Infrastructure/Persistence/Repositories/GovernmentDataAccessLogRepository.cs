using JobPlatform.GovernmentIntegration.Domain;
using JobPlatform.GovernmentIntegration.Domain.Interfaces.Repositories;

namespace JobPlatform.GovernmentIntegration.Infrastructure.Persistence.Repositories;

internal sealed class GovernmentDataAccessLogRepository(GovernmentIntegrationDbContext db) : IGovernmentDataAccessLogRepository
{
    public void Add(GovernmentDataAccessLogEntry entry) => db.GovernmentDataAccessLog.Add(entry);
}
