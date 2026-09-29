namespace JobPlatform.AccountIdentity.Application.DTOs.Accounts;

public sealed record RegisteredAccountDto(Guid AccountId, string ActorType, string Standing, string NextStep, DateTime? ActivationCodeExpiresAtUtc);
