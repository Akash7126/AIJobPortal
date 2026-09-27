using JobPlatform.SharedKernel.Domain;

namespace JobPlatform.ExternalIntegration.Domain;

/// <summary>Proposed aggregate (US-3.4.3-01/02/03/05): a served API contract version and its deprecation window. No catalogued events.</summary>
public sealed class ApiVersion : AggregateRoot<string>
{
    public static readonly IReadOnlyList<string> SupportedFormats = new[] { "json", "xml", "csv" };

    private List<string> _acceptedFormats = new() { "json" };

    private ApiVersion()
    {
    }

    public ApiVersionStatus Status { get; private set; }
    public DateTime? DeprecatedAtUtc { get; private set; }
    public DateTime? SunsetAtUtc { get; private set; }
    public IReadOnlyList<string> AcceptedFormats => _acceptedFormats;

    public static ApiVersion Release(string version) => new() { Id = version, Status = ApiVersionStatus.Active };

    /// <summary>3.4.3-05 AC-02: serving a deprecated version still succeeds (with Deprecation/Sunset headers); AC-03: two versions
    /// are served concurrently — Deprecate never touches any other ApiVersion row.</summary>
    public void Deprecate(DateTime sunsetAtUtc, DateTime nowUtc, Actor actor)
    {
        Check(Rules.AdminOnly(actor, RuleCodes.ApiAdminOnly, ErrorCodes.ApiAdminOnly));
        Check(new BusinessRule(RuleCodes.ApiNotActive, "Only an active version can be deprecated.", Status != ApiVersionStatus.Active,
            ErrorCodes.ApiInvalidTransition, BusinessRuleKind.Conflict));
        Check(new BusinessRule(RuleCodes.ApiDeprecationWindowRequired, "The sunset date must be in the future.", sunsetAtUtc <= nowUtc,
            ErrorCodes.ApiInvalidTransition, BusinessRuleKind.BusinessRule));

        Status = ApiVersionStatus.Deprecated;
        DeprecatedAtUtc = nowUtc;
        SunsetAtUtc = sunsetAtUtc;
    }

    /// <summary>INV-10: only administrators, and only after SunsetAtUtc (3.4.3-05 AC-04).</summary>
    public void Retire(Actor actor, DateTime nowUtc)
    {
        Check(Rules.AdminOnly(actor, RuleCodes.ApiAdminOnly, ErrorCodes.ApiAdminOnly));
        Check(new BusinessRule(RuleCodes.ApiNotDeprecated, "Only a deprecated version can be retired.", Status != ApiVersionStatus.Deprecated,
            ErrorCodes.ApiInvalidTransition, BusinessRuleKind.Conflict));
        Check(new BusinessRule(RuleCodes.ApiRetireBeforeSunset, "The version cannot be retired before its sunset date.",
            nowUtc < SunsetAtUtc, ErrorCodes.ApiRetireBeforeSunset, BusinessRuleKind.Conflict));

        Status = ApiVersionStatus.Retired;
    }

    /// <summary>3.4.3-02 AC-03/04: admin-only, JSON plus a documented allow-list; anything else is E-APIF-UNSUPPORTED-FORMAT.</summary>
    public void ConfigureAcceptedFormats(IReadOnlyList<string> formats, Actor actor)
    {
        Check(Rules.AdminOnly(actor, RuleCodes.ApiAdminOnly, ErrorCodes.ApiAdminOnly));
        var unsupported = formats.Where(f => !SupportedFormats.Contains(f, StringComparer.OrdinalIgnoreCase)).ToList();
        Check(new BusinessRule(RuleCodes.ApiUnsupportedFormat, $"Unsupported format(s): {string.Join(", ", unsupported)}.", unsupported.Count > 0,
            ErrorCodes.ApiUnsupportedFormat, BusinessRuleKind.InvalidInput));

        _acceptedFormats = formats.Select(f => f.ToLowerInvariant()).Distinct().ToList();
    }
}
