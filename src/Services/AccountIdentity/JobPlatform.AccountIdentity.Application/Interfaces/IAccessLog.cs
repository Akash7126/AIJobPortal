using JobPlatform.AccountIdentity.Application.Abstractions;

namespace JobPlatform.AccountIdentity.Application.Interfaces;

/// <summary>Appends authentication/authorisation decisions (US-3.1.5-03 AC-03). Never receives passwords, tokens or codes.</summary>
public interface IAccessLog
{
    Task AppendAsync(AccessLogEntry entry, CancellationToken ct = default);
}
