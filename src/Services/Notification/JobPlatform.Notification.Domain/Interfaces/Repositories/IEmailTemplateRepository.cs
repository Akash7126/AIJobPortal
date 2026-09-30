namespace JobPlatform.Notification.Domain.Interfaces.Repositories;

public interface IEmailTemplateRepository
{
    Task<EmailTemplate?> GetCurrentAsync(string code, string locale, CancellationToken ct = default);

    Task<EmailTemplate?> GetVersionAsync(string code, string locale, int version, CancellationToken ct = default);

    void Add(EmailTemplate template);
}
