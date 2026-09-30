using System.Collections.Concurrent;
using JobPlatform.Notification.Application;
using JobPlatform.Notification.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace JobPlatform.Notification.Infrastructure.Adapters;

/// <summary>
/// Deterministic e-mail/SMS provider stand-in (anti-corruption adapter behind <see cref="IEmailProvider"/> / <see cref="ISmsGateway"/>), selected by
/// Providers:Provider = Fake. It records what would have been sent (masked in logs, never the body), and can be told to time out or refuse, which is how
/// the retry and failure paths are exercised. A real SMTP / SMS-gateway adapter is a drop-in replacement of these two interfaces.
/// </summary>
public sealed class FakeProviders : IEmailProvider, ISmsGateway
{
    private readonly ConcurrentQueue<EmailEnvelope> _emails = new();
    private readonly ConcurrentQueue<SmsEnvelope> _sms = new();
    private readonly ILogger<FakeProviders> _logger;
    private int _counter;

    public FakeProviders(ILogger<FakeProviders> logger) => _logger = logger;

    public IReadOnlyCollection<EmailEnvelope> Emails => _emails.ToArray();

    public IReadOnlyCollection<SmsEnvelope> Messages => _sms.ToArray();

    /// <summary>Behaviour of the next calls: "ok" (default), "timeout" or "refuse".</summary>
    public string Mode { get; set; } = "ok";

    public Task<ProviderResult> SendAsync(EmailEnvelope email, CancellationToken ct = default)
    {
        if (Fail(out var failure))
        {
            return Task.FromResult(failure);
        }

        _emails.Enqueue(email);
        _logger.LogInformation("Fake e-mail accepted for {Recipient} (subject length {Length})", Masking.Email(email.To), email.Subject.Length);
        return Task.FromResult(ProviderResult.Ok($"fake-email-{Interlocked.Increment(ref _counter)}"));
    }

    public Task<ProviderResult> SendAsync(SmsEnvelope sms, CancellationToken ct = default)
    {
        if (Fail(out var failure))
        {
            return Task.FromResult(failure);
        }

        _sms.Enqueue(sms);
        _logger.LogInformation("Fake SMS accepted for {Recipient} from {Sender}", Masking.Mobile(sms.To), sms.SenderId);
        return Task.FromResult(ProviderResult.Ok($"fake-sms-{Interlocked.Increment(ref _counter)}"));
    }

    private bool Fail(out ProviderResult result)
    {
        result = Mode switch
        {
            "timeout" => ProviderResult.Timeout(),
            "refuse" => ProviderResult.Fail("E-NOTIF-PROVIDER-REFUSED"),
            _ => ProviderResult.Ok(string.Empty)
        };
        return Mode is "timeout" or "refuse";
    }
}

/// <summary>Dev/test contact source (Contacts:Provider = Fake): a deterministic e-mail, mobile and locale derived from the account id. BC-03 is the real source.</summary>
public sealed class FakeAccountContactApi : IAccountContactApi
{
    private readonly ConcurrentDictionary<Guid, RecipientContact?> _overrides = new();

    public void Set(Guid accountId, RecipientContact? contact) => _overrides[accountId] = contact;

    public Task<RecipientContact?> GetContactAsync(Guid accountId, CancellationToken ct = default)
    {
        if (_overrides.TryGetValue(accountId, out var custom))
        {
            return Task.FromResult(custom);
        }

        var digits = ((uint)accountId.GetHashCode() % 10_000_000).ToString("D7");
        return Task.FromResult<RecipientContact?>(new RecipientContact($"user-{accountId.ToString("N")[..8]}@example.test", $"+97059{digits}", "en"));
    }
}
