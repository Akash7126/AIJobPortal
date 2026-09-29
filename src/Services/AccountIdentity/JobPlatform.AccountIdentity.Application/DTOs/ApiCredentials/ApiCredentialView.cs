namespace JobPlatform.AccountIdentity.Application.DTOs.ApiCredentials;

public sealed record ApiCredentialView(Guid ApiCredentialId, string KeyId, string Status, IReadOnlyList<string> IpWhitelist, int MaxRequests,
    int PeriodSeconds, DateTime IssuedAtUtc, DateTime ExpiresAtUtc);
