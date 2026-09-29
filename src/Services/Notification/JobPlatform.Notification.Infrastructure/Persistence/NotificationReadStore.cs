using JobPlatform.Notification.Application;
using JobPlatform.Notification.Application.DTOs.Admin;
using JobPlatform.Notification.Application.DTOs.Delivery;
using JobPlatform.Notification.Application.DTOs.InApp;
using JobPlatform.Notification.Domain;
using JobPlatform.SharedKernel.Application.Paging;
using Microsoft.EntityFrameworkCore;

namespace JobPlatform.Notification.Infrastructure.Persistence;

/// <summary>Read side: AsNoTracking projections into DTOs (foundation section 3.5).</summary>
internal sealed class NotificationReadStore : INotificationReadStore
{
    private readonly NotificationDbContext _db;

    public NotificationReadStore(NotificationDbContext db) => _db = db;

    public async Task<PagedResult<InAppNotificationDto>> ListInAppAsync(Guid recipient, InAppStatus? status, PageRequest page, CancellationToken ct = default)
    {
        // Deleted notifications are never listed (INV-03).
        var query = _db.InAppNotifications.AsNoTracking().Where(n => n.RecipientAccountId == recipient && n.Status != InAppStatus.Deleted);
        if (status is { } s)
        {
            query = query.Where(n => n.Status == s);
        }

        var total = await query.CountAsync(ct);
        var rows = await query.OrderByDescending(n => n.CreatedAtUtc).ThenBy(n => n.Id).Skip(page.Skip).Take(page.PageSize).ToListAsync(ct);
        var codes = rows.Select(r => r.TypeCode).Distinct().ToList();
        var types = await _db.NotificationTypes.AsNoTracking().Where(t => codes.Contains(t.Id)).ToDictionaryAsync(t => t.Id, ct);
        return new PagedResult<InAppNotificationDto>(rows.Select(n =>
        {
            // INV-05: an unrecognised type gets the generic indicator, never a broken icon.
            var type = types.GetValueOrDefault(n.TypeCode) ?? NotificationType.Generic;
            return new InAppNotificationDto(n.Id, n.TypeCode, n.TitleEn, n.BodyEn, n.ActionUrl, n.Status.ToString(), n.CreatedAtUtc, n.ReadAtUtc, type.Icon, type.Colour,
                type.TextAlternative);
        }).ToList(), page.Page, page.PageSize, total);
    }

    public Task<int> CountUnreadAsync(Guid recipient, CancellationToken ct = default) =>
        _db.InAppNotifications.AsNoTracking().CountAsync(n => n.RecipientAccountId == recipient && n.Status == InAppStatus.Unread, ct);

    public async Task<PagedResult<SmsDeliveryDto>> ListSmsDeliveriesAsync(DeliveryStatus? status, PageRequest page, CancellationToken ct = default)
    {
        var query = _db.OutboundMessages.AsNoTracking().Where(m => m.Channel == Channel.Sms && m.Status != MessageStatus.Suppressed);
        if (status is { } s)
        {
            query = query.Where(m => m.DeliveryStatus == s);
        }

        var total = await query.CountAsync(ct);
        var rows = await query.OrderByDescending(m => m.CreatedAtUtc).ThenBy(m => m.Id).Skip(page.Skip).Take(page.PageSize).ToListAsync(ct);
        return new PagedResult<SmsDeliveryDto>(rows.Select(m => new SmsDeliveryDto(m.Id, "sms", m.Category, m.Status.ToString(), m.DeliveryStatus.ToString(), m.ErrorCode,
            m.CreatedAtUtc)).ToList(), page.Page, page.PageSize, total);
    }

    public async Task<NotificationDetailDto?> GetDetailAsync(Guid id, CancellationToken ct = default)
    {
        var message = await _db.OutboundMessages.AsNoTracking().FirstOrDefaultAsync(m => m.Id == id, ct);
        if (message is not null)
        {
            return new NotificationDetailDto(message.Id, message.Channel.ToString(), message.Category, message.Status.ToString(), message.DeliveryStatus.ToString(),
                message.Channel == Channel.Email ? message.Subject : null, message.CreatedAtUtc, message.SentAtUtc);
        }

        var inApp = await _db.InAppNotifications.AsNoTracking().FirstOrDefaultAsync(n => n.Id == id, ct);
        return inApp is null
            ? null
            : new NotificationDetailDto(inApp.Id, "InApp", inApp.Category, inApp.Status.ToString(), "Delivered", inApp.TitleEn, inApp.CreatedAtUtc, inApp.CreatedAtUtc);
    }
}
