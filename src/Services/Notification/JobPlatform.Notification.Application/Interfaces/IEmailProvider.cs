namespace JobPlatform.Notification.Application.Interfaces;

public interface IEmailProvider
{
    Task<ProviderResult> SendAsync(EmailEnvelope email, CancellationToken ct = default);
}
