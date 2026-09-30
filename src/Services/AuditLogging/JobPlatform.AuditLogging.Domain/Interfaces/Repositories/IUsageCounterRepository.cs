namespace JobPlatform.AuditLogging.Domain.Interfaces.Repositories;

public interface IUsageCounterRepository
{
    Task<IntegrationUsageDaily?> GetAsync(Guid partnerId, DateOnly day, CancellationToken ct = default);

    void Add(IntegrationUsageDaily counter);
}
