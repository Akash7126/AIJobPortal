namespace JobPlatform.Notification.Application.Interfaces;

public interface ISmsGateway
{
    Task<ProviderResult> SendAsync(SmsEnvelope sms, CancellationToken ct = default);
}
