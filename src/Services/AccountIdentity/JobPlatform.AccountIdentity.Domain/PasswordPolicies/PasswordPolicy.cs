using JobPlatform.AccountIdentity.Domain.Common;
using JobPlatform.SharedKernel.Domain;

namespace JobPlatform.AccountIdentity.Domain.PasswordPolicies;

public sealed record PasswordPolicyConfiguredDomainEvent(int PolicyVersion, DateTime At) : DomainEvent(At);

public static class PasswordPolicyRuleCodes
{
    public const string WeakPassword = "AI.PasswordPolicy.WEAK_PASSWORD";
    public const string AdminOnly = "AI.PasswordPolicy.ADMIN_ONLY";
    public const string InvalidParameters = "AI.PasswordPolicy.INVALID_PARAMETERS";
}

/// <summary>Field-level violation codes returned for a rejected password (never echo the password itself).</summary>
public static class PasswordViolations
{
    public const string MinLength = "VAL.Password.MinLength";
    public const string MaxLength = "VAL.Password.MaxLength";
    public const string Uppercase = "VAL.Password.Uppercase";
    public const string Lowercase = "VAL.Password.Lowercase";
    public const string Digit = "VAL.Password.Digit";
}

/// <summary>
/// Singleton, versioned password policy (US-3.1.5-02). A change applies at each user's next password change; nobody is forced.
/// </summary>
public sealed class PasswordPolicy : AggregateRoot<Guid>
{
    public static readonly Guid SingletonId = new("0f1b6a3e-0000-4000-8000-000000000001");

    private PasswordPolicy()
    {
    }

    public int MinLength { get; private set; }
    public bool RequireUpper { get; private set; }
    public bool RequireLower { get; private set; }
    public bool RequireDigit { get; private set; }

    /// <summary>Policy revision, starts at 1 and increments on every configuration change.</summary>
    public int PolicyVersion { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }
    public Guid? UpdatedBy { get; private set; }

    /// <summary>Defaults from assumption A-02-014: at least 8 characters, one uppercase, one lowercase, one digit.</summary>
    public static PasswordPolicy CreateDefault(TimeProvider clock) => new()
    {
        Id = SingletonId,
        MinLength = AccountDefaults.DefaultPasswordMinLength,
        RequireUpper = true,
        RequireLower = true,
        RequireDigit = true,
        PolicyVersion = 1,
        UpdatedAtUtc = clock.GetUtcNow().UtcDateTime
    };

    public void Configure(Actor admin, int minLength, bool requireUpper, bool requireLower, bool requireDigit, TimeProvider clock)
    {
        Guard.Ensure(admin.IsAdministrator, PasswordPolicyRuleCodes.AdminOnly, "Administrator role required.", ErrorCodes.AuthForbidden,
            BusinessRuleKind.Forbidden);
        Guard.Ensure(minLength is >= 8 and <= AccountDefaults.MaxPasswordLength && (requireUpper || requireLower || requireDigit),
            PasswordPolicyRuleCodes.InvalidParameters, "Minimum length must be 8-128 and at least one character class is required.",
            ErrorCodes.AuthInvalidField, BusinessRuleKind.InvalidInput);

        MinLength = minLength;
        RequireUpper = requireUpper;
        RequireLower = requireLower;
        RequireDigit = requireDigit;
        PolicyVersion++;
        UpdatedAtUtc = clock.GetUtcNow().UtcDateTime;
        UpdatedBy = admin.Id;
        Raise(new PasswordPolicyConfiguredDomainEvent(PolicyVersion, UpdatedAtUtc));
    }

    public IReadOnlyList<string> Validate(string? password)
    {
        var violations = new List<string>();
        var value = password ?? string.Empty;
        if (value.Length < MinLength)
        {
            violations.Add(PasswordViolations.MinLength);
        }

        if (value.Length > AccountDefaults.MaxPasswordLength)
        {
            violations.Add(PasswordViolations.MaxLength);
        }

        if (RequireUpper && !value.Any(char.IsUpper))
        {
            violations.Add(PasswordViolations.Uppercase);
        }

        if (RequireLower && !value.Any(char.IsLower))
        {
            violations.Add(PasswordViolations.Lowercase);
        }

        if (RequireDigit && !value.Any(char.IsDigit))
        {
            violations.Add(PasswordViolations.Digit);
        }

        return violations;
    }

    /// <summary>Throws INV-07 (E-AAFR-INVALID-FIELD) listing which policy rules were violated.</summary>
    public void EnsureCompliant(string? password)
    {
        var violations = Validate(password);
        if (violations.Count > 0)
        {
            throw new BusinessRuleViolationException(PasswordPolicyRuleCodes.WeakPassword, "The password does not meet the password policy.",
                ErrorCodes.AuthInvalidField, BusinessRuleKind.InvalidInput,
                new Dictionary<string, object?> { ["field"] = "password", ["violations"] = violations.ToArray() });
        }
    }
}
