namespace JobPlatform.Notification.Domain.Interfaces.Repositories;

public interface IJobConfirmationRepository
{
    Task<JobConfirmation?> GetAsync(Guid sourcePlatformId, string sourceJobId, CancellationToken ct = default);

    void Add(JobConfirmation confirmation);
}
