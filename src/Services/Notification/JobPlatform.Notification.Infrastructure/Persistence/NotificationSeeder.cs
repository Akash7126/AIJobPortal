using JobPlatform.BuildingBlocks.Infrastructure.Interfaces.Persistence;
using JobPlatform.Notification.Domain;
using Microsoft.EntityFrameworkCore;

namespace JobPlatform.Notification.Infrastructure.Persistence;

/// <summary>Idempotent seed: essential-SMS policy v1 (OTP, password reset), the built-in notification types and the default e-mail templates.</summary>
public sealed class NotificationSeeder : IDbSeeder<NotificationDbContext>
{
    private static readonly Actor System = new(Guid.Empty, true);
    private readonly TimeProvider _clock;

    public NotificationSeeder(TimeProvider clock) => _clock = clock;

    public async Task SeedAsync(NotificationDbContext db, CancellationToken ct)
    {
        var now = _clock.GetUtcNow().UtcDateTime;
        if (!await db.SmsPolicies.AnyAsync(ct))
        {
            db.SmsPolicies.Add(SmsPolicy.Initial(now));
        }

        var types = new (string Code, string Icon, string Colour, string Alt, bool Mandatory)[]
        {
            ("saved-search-match", "search", "#1D4ED8", "Saved search match", false),
            ("weekly-recommendation", "star", "#7C3AED", "Weekly job recommendation", false),
            ("job-confirmation", "check", "#15803D", "Job received", false),
            ("welcome", "user", "#1D4ED8", "Welcome", false),
            ("otp", "shield", "#B91C1C", "Security code", true),
            ("password-reset", "shield", "#B91C1C", "Password reset", true)
        };
        foreach (var t in types.Where(t => !db.NotificationTypes.Local.Any(x => x.Id == t.Code)))
        {
            if (!await db.NotificationTypes.AnyAsync(x => x.Id == t.Code, ct))
            {
                db.NotificationTypes.Add(NotificationType.Define(System, t.Code, t.Icon, t.Colour, t.Alt, t.Mandatory));
            }
        }

        await AddTemplate(db, "welcome", "Welcome to JobPlatform", "Hello {{name}}, your account is now active.", new Dictionary<string, string> { ["name"] = "there" }, now, ct);
        await AddTemplate(db, "job-confirmation", "Job received: {{platformJobId}}",
            "Your job {{sourceJobId}} was received. The platform job id is {{platformJobId}}.",
            new Dictionary<string, string> { ["platformJobId"] = "(pending)", ["sourceJobId"] = "your job" }, now, ct);
        await db.SaveChangesAsync(ct);
    }

    private static async Task AddTemplate(NotificationDbContext db, string code, string subject, string body, Dictionary<string, string> placeholders, DateTime now, CancellationToken ct)
    {
        if (!await db.EmailTemplates.AnyAsync(t => t.Code == code && t.Locale == "en", ct))
        {
            db.EmailTemplates.Add(EmailTemplate.Create(code, "en", subject, body, placeholders, now));
        }
    }
}
