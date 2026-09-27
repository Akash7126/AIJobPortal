using System.Text.Json.Serialization;
using JobPlatform.SharedKernel.Common.Enums;

namespace JobPlatform.AccountIdentity.Api.Contracts;

// Request bodies. They are mapped to commands in the controllers; validation happens in the application pipeline.

public sealed record RegisterJobSeekerRequest(string FullName, string Mobile, string? Email, string Password, string? PreferredLanguage);

public sealed record RegisterEmployerRequest(string CompanyName, string Email, string Mobile, string CompanyId, string RegistrationNumber,
    string Password, int Level = 1);

public sealed record RegisterPartnerRequest(string OrganisationName, string ContactEmail, string Mobile, string Identity, string Password);

public sealed record ActivateAccountRequest(string Code);

public sealed record LoginRequest(string Username, string? Password, string Mechanism = "password", ActorType? ActorType = null, string? MfaCode = null,
    string? EmailCode = null);

public sealed record MfaEnrollRequest(string MfaToken);

public sealed record MfaVerifyRequest(string MfaToken, string Code);

public sealed record RefreshRequest(string RefreshToken);

public sealed record EmailVerificationRequest(Guid AccountId, string Token);

public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);

public sealed record ConfigurePasswordPolicyRequest(int MinLength, bool RequireUpper, bool RequireLower, bool RequireDigit);

public sealed record ConfigureSessionTimeoutRequest(int IdleTimeoutMinutes);

public sealed record ReasonRequest(string Reason);

public sealed record IssueApiCredentialRequest(IReadOnlyList<string>? IpWhitelist, int? MaxRequests, int? PeriodSeconds, DateTime? ExpiresAtUtc);

public sealed record RecordConsentRequest(Guid? GuestId, string PolicyVersion, bool Analytics, bool Preferences, bool Marketing, string? Locale);

public sealed record DeactivationRequest(string Kind, string Reason);

/// <summary>OAuth 2.0 token endpoint body (form-encoded per RFC 6749, JSON also accepted).</summary>
public sealed record OAuthTokenRequest(
    [property: JsonPropertyName("grant_type")] string? GrantType,
    [property: JsonPropertyName("client_id")] string? ClientId,
    [property: JsonPropertyName("client_secret")] string? ClientSecret);

public sealed record OAuthTokenResponse(
    [property: JsonPropertyName("access_token")] string AccessToken,
    [property: JsonPropertyName("token_type")] string TokenType,
    [property: JsonPropertyName("expires_in")] int ExpiresIn,
    [property: JsonPropertyName("scope")] string Scope);
