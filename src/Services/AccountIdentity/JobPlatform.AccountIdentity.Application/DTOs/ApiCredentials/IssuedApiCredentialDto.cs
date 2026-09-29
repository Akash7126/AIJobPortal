namespace JobPlatform.AccountIdentity.Application.DTOs.ApiCredentials;

/// <param name="ClientSecret">Shown exactly once, in this response. Only its hash is stored and it never appears in events or logs.</param>
public sealed record IssuedApiCredentialDto(Guid ApiCredentialId, string ClientId, string ClientSecret, DateTime ExpiresAtUtc, int MaxRequests,
    int PeriodSeconds, IReadOnlyList<string> IpWhitelist, Guid? RevokedPreviousCredentialId);
