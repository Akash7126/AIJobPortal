namespace JobPlatform.Notification.Application.Interfaces;

/// <summary>Signed, tamper-evident unsubscribe tokens (no login needed, 3.6.1-05).</summary>
public interface IUnsubscribeTokens
{
    string Create(Guid accountId, string category);

    bool TryParse(string token, out Guid accountId, out string category);
}
