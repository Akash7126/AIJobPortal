namespace JobPlatform.AccountIdentity.Application.DTOs.Authentication;

public sealed record AuthenticationResultDto(string Status, TokenPairDto? Tokens, string? MfaToken, DateTime? MfaTokenExpiresAtUtc, bool MustChangePassword);
