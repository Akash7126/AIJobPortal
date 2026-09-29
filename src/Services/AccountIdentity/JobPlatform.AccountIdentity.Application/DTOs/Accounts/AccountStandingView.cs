using JobPlatform.SharedKernel.Common.Enums;

namespace JobPlatform.AccountIdentity.Application.DTOs.Accounts;

public sealed record AccountStandingView(
    Guid AccountId,
    ActorType ActorType,
    string DisplayName,
    string? Email,
    string Mobile,
    string Standing,
    bool IsLocked,
    DateTime? LockedUntilUtc,
    bool MfaEnabled,
    bool EmailVerified,
    bool MustChangePassword,
    DateTime CreatedAtUtc,
    DateTime? ActivatedAtUtc,
    IReadOnlyList<AccountStatusChangeView> History,
    IReadOnlyList<string> AvailableActions,
    string ETag = "");
