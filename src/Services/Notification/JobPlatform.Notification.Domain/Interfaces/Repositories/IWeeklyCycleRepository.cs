namespace JobPlatform.Notification.Domain.Interfaces.Repositories;

public interface IWeeklyCycleRepository
{
    Task<bool> ExistsAsync(Guid accountId, string isoWeek, CancellationToken ct = default);

    void Add(WeeklyCycle cycle);
}
