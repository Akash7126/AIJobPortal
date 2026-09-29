using JobPlatform.AccountIdentity.Application.DTOs.Consent;
using JobPlatform.SharedKernel.Application.Abstractions;

namespace JobPlatform.AccountIdentity.Application.Commands.Consent;

/// <param name="GuestId">Anonymous cookie id; a new one is minted when the guest has none yet.</param>
public sealed record RecordPrivacyConsentCommand(Guid? GuestId, string PolicyVersion, bool Analytics, bool Preferences, bool Marketing, string? Locale,
    string? IdempotencyKey) : ICommand<ConsentStatusDto>, IIdempotentCommand;
