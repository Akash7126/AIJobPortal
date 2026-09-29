namespace JobPlatform.AccountIdentity.Application.DTOs.Authentication;

public sealed record MfaEnrollmentDto(string Secret, string ProvisioningUri);
