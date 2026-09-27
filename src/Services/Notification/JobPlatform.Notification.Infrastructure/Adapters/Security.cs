using System.Security.Cryptography;
using System.Text;
using JobPlatform.Notification.Application;
using Microsoft.Extensions.Options;

namespace JobPlatform.Notification.Infrastructure.Adapters;

public sealed class NotificationSecurityOptions
{
    public const string SectionName = "NotificationSecurity";

    /// <summary>HMAC key of unsubscribe tokens and provider webhook signatures. Placeholder here; set a real secret through the environment.</summary>
    public string SigningKey { get; set; } = "dev-only-notification-signing-key-change-me";

    public string WebhookSecret { get; set; } = "dev-only-webhook-secret-change-me";
}

/// <summary>Tamper-evident unsubscribe token: base64url(accountId|category) plus a truncated HMAC-SHA256. No login needed to use it (3.6.1-05).</summary>
public sealed class HmacUnsubscribeTokens : IUnsubscribeTokens
{
    private readonly byte[] _key;

    public HmacUnsubscribeTokens(IOptions<NotificationSecurityOptions> options) => _key = Encoding.UTF8.GetBytes(options.Value.SigningKey);

    public string Create(Guid accountId, string category)
    {
        var payload = Base64Url(Encoding.UTF8.GetBytes($"{accountId:N}|{category}"));
        return $"{payload}.{Sign(payload)}";
    }

    public bool TryParse(string token, out Guid accountId, out string category)
    {
        accountId = Guid.Empty;
        category = string.Empty;
        var parts = token.Split('.');
        if (parts.Length != 2 || !CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(Sign(parts[0])), Encoding.ASCII.GetBytes(parts[1])))
        {
            return false;
        }

        try
        {
            var decoded = Encoding.UTF8.GetString(FromBase64Url(parts[0])).Split('|');
            if (decoded.Length != 2 || !Guid.TryParseExact(decoded[0], "N", out accountId))
            {
                return false;
            }

            category = decoded[1];
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private string Sign(string payload) => Base64Url(HMACSHA256.HashData(_key, Encoding.UTF8.GetBytes(payload))[..16]);

    private static string Base64Url(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static byte[] FromBase64Url(string value)
    {
        var padded = value.Replace('-', '+').Replace('_', '/');
        return Convert.FromBase64String(padded.PadRight(padded.Length + (4 - padded.Length % 4) % 4, '='));
    }
}

/// <summary>Provider webhooks carry an HMAC-SHA256 hex signature of the raw body (X-Signature); anything else is refused with 401.</summary>
public sealed class HmacWebhookVerifier : IWebhookVerifier
{
    private readonly byte[] _key;

    public HmacWebhookVerifier(IOptions<NotificationSecurityOptions> options) => _key = Encoding.UTF8.GetBytes(options.Value.WebhookSecret);

    public bool IsValid(string body, string? signature)
    {
        if (string.IsNullOrWhiteSpace(signature))
        {
            return false;
        }

        var expected = Convert.ToHexString(HMACSHA256.HashData(_key, Encoding.UTF8.GetBytes(body)));
        return CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(expected.ToLowerInvariant()), Encoding.ASCII.GetBytes(signature.Trim().ToLowerInvariant()));
    }

    public static string Sign(string secret, string body) => Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(body))).ToLowerInvariant();
}
