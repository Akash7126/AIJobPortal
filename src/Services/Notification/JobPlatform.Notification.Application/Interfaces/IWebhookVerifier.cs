namespace JobPlatform.Notification.Application.Interfaces;

/// <summary>Verifies the HMAC signature of provider webhooks (3.6.3-04).</summary>
public interface IWebhookVerifier
{
    bool IsValid(string body, string? signature);
}
