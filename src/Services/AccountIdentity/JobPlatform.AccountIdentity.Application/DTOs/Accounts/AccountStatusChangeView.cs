namespace JobPlatform.AccountIdentity.Application.DTOs.Accounts;

public sealed record AccountStatusChangeView(string? From, string To, Guid By, string? Reason, DateTime AtUtc);
