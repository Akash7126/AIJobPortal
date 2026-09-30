using JobPlatform.ExternalIntegration.Domain;
using JobPlatform.ExternalIntegration.Domain.Interfaces.Repositories;

namespace JobPlatform.ExternalIntegration.Infrastructure.Persistence.Repositories;

internal sealed class ApiSchemaAccessLogRepository(ExternalIntegrationDbContext db) : IApiSchemaAccessLogRepository
{
    public void Add(ApiSchemaAccessLog log) => db.ApiSchemaAccessLogs.Add(log);
}
