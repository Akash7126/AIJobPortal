namespace JobPlatform.AccountIdentity.Application.DTOs.Authentication;

public sealed record TokenPairDto(string AccessToken, string RefreshToken, string TokenType, int ExpiresInSeconds, DateTime ExpiresAtUtc);
