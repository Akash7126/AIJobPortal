namespace JobPlatform.AccountIdentity.Application.DTOs.Consent;

public sealed record ConsentChoicesDto(bool Necessary, bool Analytics, bool Preferences, bool Marketing);
