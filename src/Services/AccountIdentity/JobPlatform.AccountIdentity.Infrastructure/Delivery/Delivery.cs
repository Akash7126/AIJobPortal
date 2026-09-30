using System.Collections.Concurrent;
using JobPlatform.AccountIdentity.Application.Abstractions;
using JobPlatform.AccountIdentity.Application.Interfaces;
using JobPlatform.AccountIdentity.Infrastructure.Persistence;
using JobPlatform.SharedKernel.Common.Enums;
using JobPlatform.SharedKernel.Common.ValueObjects;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace JobPlatform.AccountIdentity.Infrastructure.Delivery;

public sealed class DeliveryOptions
{
    public const string SectionName = "Delivery";

    /// <summary>"Log" (default; masked log line only) or "Capture" (in-memory outbox for tests and demos).</summary>
    public string Provider { get; set; } = "Log";

    /// <summary>Development only: also log the code itself at Debug level so a developer can complete activation without an SMS gateway.</summary>
    public bool LogCodesInDevelopment { get; set; }
}

/// <summary>
/// Q-03: BC-13 (Notification) does not consume any OTP/e-mail-verification message yet, so delivery is behind ports with this
/// dev adapter. It logs a masked recipient and never the code (unless the explicit development flag is set).
/// </summary>
public sealed class LoggingMessageSender : IOtpSender, IEmailVerificationSender
{
    private readonly ILogger<LoggingMessageSender> _logger;
    private readonly bool _logCodes;

    public LoggingMessageSender(ILogger<LoggingMessageSender> logger, IOptions<DeliveryOptions> options)
    {
        _logger = logger;
        _logCodes = options.Value.LogCodesInDevelopment;
    }

    public Task SendActivationCodeAsync(MobileNumber mobile, string code, Language language, CancellationToken ct = default)
    {
        _logger.LogInformation("[dev delivery] activation code sent to {Recipient} ({Language})", mobile.Masked, language);
        if (_logCodes)
        {
            _logger.LogDebug("[dev delivery] activation code for {Recipient}: {Code}", mobile.Masked, code);
        }

        return Task.CompletedTask;
    }

    public Task SendVerificationAsync(Email email, Guid accountId, string token, Language language, CancellationToken ct = default)
    {
        _logger.LogInformation("[dev delivery] e-mail verification sent for account {AccountId} ({Language})", accountId, language);
        if (_logCodes)
        {
            _logger.LogDebug("[dev delivery] e-mail verification token for {AccountId}: {Token}", accountId, token);
        }

        return Task.CompletedTask;
    }

    public Task SendLoginCodeAsync(Email email, string code, Language language, CancellationToken ct = default)
    {
        _logger.LogInformation("[dev delivery] e-mail login code sent ({Language})", language);
        if (_logCodes)
        {
            _logger.LogDebug("[dev delivery] e-mail login code: {Code}", code);
        }

        return Task.CompletedTask;
    }
}

public sealed record CapturedMessage(string Kind, string Recipient, string? Code, Guid? AccountId, Language Language);

/// <summary>Keeps sent codes in memory so integration tests and demos can complete flows without a gateway. Not for production.</summary>
public sealed class CapturingMessageSender : IOtpSender, IEmailVerificationSender
{
    private readonly ConcurrentQueue<CapturedMessage> _messages = new();

    public IReadOnlyCollection<CapturedMessage> Messages => _messages.ToArray();

    public CapturedMessage? LastFor(string kind, string recipient) =>
        _messages.Reverse().FirstOrDefault(m => m.Kind == kind && m.Recipient == recipient);

    public Task SendActivationCodeAsync(MobileNumber mobile, string code, Language language, CancellationToken ct = default)
    {
        _messages.Enqueue(new CapturedMessage("activation-code", mobile.Value, code, null, language));
        return Task.CompletedTask;
    }

    public Task SendVerificationAsync(Email email, Guid accountId, string token, Language language, CancellationToken ct = default)
    {
        _messages.Enqueue(new CapturedMessage("email-verification", email.Value, token, accountId, language));
        return Task.CompletedTask;
    }

    public Task SendLoginCodeAsync(Email email, string code, Language language, CancellationToken ct = default)
    {
        _messages.Enqueue(new CapturedMessage("login-code", email.Value, code, null, language));
        return Task.CompletedTask;
    }
}

/// <summary>
/// Appends decisions to identity.AccessLog. Inside a running transaction the row commits with the command; otherwise (authorisation
/// checks run before the unit of work starts) it is written immediately so denied requests are recorded too.
/// </summary>
public sealed class EfAccessLog : IAccessLog
{
    private readonly IdentityDbContext _db;

    public EfAccessLog(IdentityDbContext db) => _db = db;

    public async Task AppendAsync(AccessLogEntry entry, CancellationToken ct = default)
    {
        _db.AccessLog.Add(new AccessLogRecord
        {
            Id = Guid.NewGuid(),
            AtUtc = entry.AtUtc,
            AccountId = entry.AccountId,
            Action = Truncate(entry.Action, 200),
            Resource = Truncate(entry.Resource, 200),
            Decision = Truncate(entry.Decision, 20),
            Reason = entry.Reason is null ? null : Truncate(entry.Reason, 200),
            IpAddress = entry.IpAddress is null ? null : Truncate(entry.IpAddress, 64)
        });

        if (_db.Database.CurrentTransaction is null)
        {
            await _db.SaveChangesAsync(ct);
        }
    }

    private static string Truncate(string value, int max) => value.Length <= max ? value : value[..max];
}
