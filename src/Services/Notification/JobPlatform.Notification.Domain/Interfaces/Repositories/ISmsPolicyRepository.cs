namespace JobPlatform.Notification.Domain.Interfaces.Repositories;

public interface ISmsPolicyRepository
{
    Task<SmsPolicy> GetCurrentAsync(CancellationToken ct = default);

    void Add(SmsPolicy policy);
}
