namespace JobPlatform.GovernmentIntegration.Domain.Interfaces.Repositories;

public interface IGovernmentDataAccessLogRepository
{
    void Add(GovernmentDataAccessLogEntry entry);
}
