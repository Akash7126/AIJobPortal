using JobPlatform.Notification.Domain;
using JobPlatform.Notification.Domain.Interfaces.Repositories;
using Microsoft.EntityFrameworkCore;

namespace JobPlatform.Notification.Infrastructure.Persistence.Repositories;

internal sealed class EmailTemplateRepository : IEmailTemplateRepository
{
    private readonly NotificationDbContext _db;

    public EmailTemplateRepository(NotificationDbContext db) => _db = db;

    public Task<EmailTemplate?> GetCurrentAsync(string code, string locale, CancellationToken ct = default) =>
        _db.EmailTemplates.Where(t => t.Code == code && t.Locale == locale).OrderByDescending(t => t.Version).FirstOrDefaultAsync(ct);

    public Task<EmailTemplate?> GetVersionAsync(string code, string locale, int version, CancellationToken ct = default) =>
        _db.EmailTemplates.FirstOrDefaultAsync(t => t.Code == code && t.Locale == locale && t.Version == version, ct);

    public void Add(EmailTemplate template) => _db.EmailTemplates.Add(template);
}
