using JobPlatform.SharedKernel.Common.Enums;

namespace JobPlatform.AccountIdentity.Application.Abstractions;

public sealed record UserTokenRequest(Guid AccountId, ActorType ActorType, IReadOnlyCollection<Guid> RoleIds, Guid SessionId, bool MfaVerified,
    bool MustChangePassword);

public sealed record ClientTokenRequest(Guid PartnerAccountId, string ClientId, IReadOnlyCollection<string> Scopes, IReadOnlyCollection<Guid> RoleIds,
    ActorType ActorType, TimeSpan Lifetime);

public sealed record IssuedAccessToken(string Token, string TokenId, DateTime ExpiresAtUtc);

public sealed record MfaChallengeToken(string Token, DateTime ExpiresAtUtc);

public sealed record AccessLogEntry(DateTime AtUtc, Guid? AccountId, string Action, string Resource, string Decision, string? Reason, string? IpAddress);

public sealed record ServiceClient(string ClientId, IReadOnlyCollection<string> Scopes);

public sealed class ConsentOptions
{
    public const string SectionName = "Consent";

    public string CurrentPolicyVersion { get; set; } = "2026-01";
    public string PrivacyPolicyUrl { get; set; } = "/privacy-policy";
    public string BannerTextEn { get; set; } = "We use essential cookies to run the platform. With your consent we also use analytics, preference and marketing cookies. See our privacy policy.";
    public string BannerTextAr { get; set; } = "نستخدم ملفات تعريف الارتباط الأساسية لتشغيل المنصة. وبموافقتك نستخدم أيضاً ملفات التحليلات والتفضيلات والتسويق. اطلع على سياسة الخصوصية.";
}

public sealed record AccountListFilter(ActorType? ActorType, string? Standing, string? Search);

public sealed record ConsentDecisionView(Guid GuestId, string PolicyVersion, bool Analytics, bool Preferences, bool Marketing, DateTime DecidedAtUtc,
    string Locale);
