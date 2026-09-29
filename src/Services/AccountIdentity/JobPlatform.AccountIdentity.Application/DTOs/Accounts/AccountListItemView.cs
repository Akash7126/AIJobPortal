using JobPlatform.SharedKernel.Common.Enums;

namespace JobPlatform.AccountIdentity.Application.DTOs.Accounts;

public sealed record AccountListItemView(Guid AccountId, ActorType ActorType, string DisplayName, string? Email, string Mobile, string Standing,
    DateTime CreatedAtUtc);
