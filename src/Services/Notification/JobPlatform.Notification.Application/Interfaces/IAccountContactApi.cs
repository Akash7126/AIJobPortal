namespace JobPlatform.Notification.Application.Interfaces;

/// <summary>Recipient contact data lives in BC-03 and is never in events: fetched at send time (GET /internal/v1/accounts/{id}/contact).</summary>
public interface IAccountContactApi
{
    Task<RecipientContact?> GetContactAsync(Guid accountId, CancellationToken ct = default);
}
