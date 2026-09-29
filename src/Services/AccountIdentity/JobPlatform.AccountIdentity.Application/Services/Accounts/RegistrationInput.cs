using JobPlatform.AccountIdentity.Domain.Accounts;
using JobPlatform.SharedKernel.Common.Enums;

namespace JobPlatform.AccountIdentity.Application.Services.Accounts;

/// <summary>Input of <see cref="RegistrationWorkflow"/>, common to the three registration commands.</summary>
internal sealed record RegistrationInput(ActorType ActorType, string DisplayName, string Mobile, string? Email, string? IdentityKey,
    string? RegistrationNumber, RegistrationLevel Level, string Password, string? PreferredLanguage);
