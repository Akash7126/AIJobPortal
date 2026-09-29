namespace JobPlatform.AccountIdentity.Application.DTOs.Administration;

public sealed record PasswordPolicyView(int MinLength, bool RequireUpper, bool RequireLower, bool RequireDigit, int PolicyVersion, DateTime UpdatedAtUtc,
    string ETag = "");
