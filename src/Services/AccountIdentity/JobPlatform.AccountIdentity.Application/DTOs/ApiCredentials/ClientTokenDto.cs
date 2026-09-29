namespace JobPlatform.AccountIdentity.Application.DTOs.ApiCredentials;

public sealed record ClientTokenDto(string AccessToken, string TokenType, int ExpiresIn, string Scope);
