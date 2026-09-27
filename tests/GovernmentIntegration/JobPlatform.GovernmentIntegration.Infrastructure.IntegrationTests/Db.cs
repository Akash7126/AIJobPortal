using JobPlatform.GovernmentIntegration.Application.Events;
using JobPlatform.GovernmentIntegration.Domain.Common;
using JobPlatform.GovernmentIntegration.Infrastructure.Persistence;
using JobPlatform.TestSupport;

namespace JobPlatform.GovernmentIntegration.Infrastructure.IntegrationTests;

internal static class Db
{
    public static readonly DateTime T0 = new(2026, 4, 1, 9, 0, 0, DateTimeKind.Utc);
    public static readonly Actor Admin = new(Guid.NewGuid(), true);

    public static SqliteTestDatabase<GovernmentIntegrationDbContext> New() =>
        new(o => new GovernmentIntegrationDbContext(o), new GovernmentIntegrationEventMapper());
}
