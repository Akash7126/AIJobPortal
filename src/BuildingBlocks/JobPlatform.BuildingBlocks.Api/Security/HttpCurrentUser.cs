using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using JobPlatform.SharedKernel.Application.Interfaces.Ports;
using JobPlatform.SharedKernel.Common.Enums;
using JobPlatform.SharedKernel.Security;
using Microsoft.AspNetCore.Http;

namespace JobPlatform.BuildingBlocks.Api.Security;

/// <summary>The caller as seen by the application layer, built from the validated JWT (issued by BC-03) and the request.</summary>
public sealed class HttpCurrentUser : ICurrentUser
{
    public const string DeviceHeader = "X-Device-Id";

    private readonly IHttpContextAccessor _accessor;

    public HttpCurrentUser(IHttpContextAccessor accessor) => _accessor = accessor;

    private HttpContext? Http => _accessor.HttpContext;

    private ClaimsPrincipal? Principal => Http?.User;

    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated == true;

    public Guid? UserId => Guid.TryParse(Principal?.FindFirstValue("sub"), out var id) ? id : null;

    public ActorType? ActorType => Enum.TryParse<ActorType>(Principal?.FindFirstValue(AppClaimTypes.ActorType), out var type) ? type : null;

    public IReadOnlyCollection<Guid> RoleIds =>
        Principal?.FindAll(AppClaimTypes.RoleId).Select(c => Guid.TryParse(c.Value, out var id) ? id : Guid.Empty).Where(id => id != Guid.Empty).ToArray()
        ?? Array.Empty<Guid>();

    public Guid? SessionId => Guid.TryParse(Principal?.FindFirstValue(AppClaimTypes.SessionId), out var id) ? id : null;

    public bool MfaVerified => Principal?.FindAll(AppClaimTypes.Amr).Any(c => c.Value == AppClaimTypes.MfaValue) == true;

    public bool MustChangePassword => Principal?.FindFirstValue(AppClaimTypes.MustChangePassword) == "true";

    public string? ClientId => Principal?.FindFirstValue(AppClaimTypes.ClientId);

    public IReadOnlyCollection<string> Scopes =>
        Principal?.FindFirstValue(AppClaimTypes.Scope)?.Split(' ', StringSplitOptions.RemoveEmptyEntries) ?? Array.Empty<string>();

    public string? TokenId => Principal?.FindFirstValue("jti");

    public string? IpAddress => Http?.Connection.RemoteIpAddress?.ToString();

    /// <summary>Rate-limit source = client IP + optional device id, hashed so raw values never become cache keys.</summary>
    public string SourceKey
    {
        get
        {
            var raw = $"{IpAddress ?? "unknown"}|{Http?.Request.Headers[DeviceHeader].FirstOrDefault()}";
            return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw)))[..16];
        }
    }

    public Language Language
    {
        get
        {
            var header = Http?.Request.Headers.AcceptLanguage.ToString();
            if (string.IsNullOrWhiteSpace(header))
            {
                return Language.En;
            }

            foreach (var part in header.Split(','))
            {
                var tag = part.Split(';')[0].Trim();
                if (tag.StartsWith("ar", StringComparison.OrdinalIgnoreCase))
                {
                    return Language.Ar;
                }

                if (tag.StartsWith("en", StringComparison.OrdinalIgnoreCase))
                {
                    return Language.En;
                }
            }

            return Language.En;
        }
    }
}

/// <summary>Localised error messages from an in-code catalogue (code to Arabic/English text); unknown codes fall back to the neutral message.</summary>
public sealed class CatalogErrorMessageLocalizer : IErrorMessageLocalizer
{
    private readonly IReadOnlyDictionary<string, (string En, string Ar)> _catalog;

    public CatalogErrorMessageLocalizer(IEnumerable<IReadOnlyDictionary<string, (string En, string Ar)>> catalogs)
    {
        var merged = new Dictionary<string, (string En, string Ar)>(StringComparer.Ordinal);
        foreach (var catalog in catalogs.Prepend(CommonErrorMessages.Catalog))
        {
            foreach (var (code, text) in catalog)
            {
                merged[code] = text;
            }
        }

        _catalog = merged;
    }

    public string Localize(string code, string fallback, Language language) =>
        _catalog.TryGetValue(code, out var text) ? (language == Language.Ar ? text.Ar : text.En) : fallback;
}

/// <summary>Messages of the platform-wide codes every service can return (auth, validation, concurrency, idempotency).</summary>
public static class CommonErrorMessages
{
    public static readonly IReadOnlyDictionary<string, (string En, string Ar)> Catalog = new Dictionary<string, (string En, string Ar)>
    {
        ["E-AAFR-UNAUTHORIZED"] = ("Authentication is required.", "المصادقة مطلوبة."),
        ["E-AAFR-FORBIDDEN"] = ("You are not allowed to perform this action.", "غير مسموح لك بتنفيذ هذا الإجراء."),
        ["E-AAFR-SESSION-EXPIRED"] = ("Your session has expired. Please sign in again.", "انتهت جلستك. يرجى تسجيل الدخول مجددا."),
        ["E-AAFR-PASSWORD-CHANGE-REQUIRED"] = ("You must change your password first.", "يجب تغيير كلمة المرور أولا."),
        ["E-RATE-LIMITED"] = ("Too many requests. Try again later.", "طلبات كثيرة جدا. حاول لاحقا."),
        ["VAL.INVALID_REQUEST"] = ("One or more validation errors occurred.", "يوجد خطأ أو أكثر في البيانات المدخلة."),
        ["E-CONCURRENCY-CONFLICT"] = ("The resource was modified by another request. Reload and try again.", "تم تعديل المورد بواسطة طلب آخر. أعد التحميل وحاول مجددا."),
        ["E-PRECONDITION-FAILED"] = ("The resource changed since it was read. Reload it and retry.", "تغير المورد منذ قراءته. أعد تحميله وحاول مجددا."),
        ["E-DUPLICATE"] = ("A record with the same unique values already exists.", "يوجد سجل بنفس القيم الفريدة."),
        ["E-IDEMPOTENCY-KEY-REUSED"] = ("The Idempotency-Key was already used with a different request.", "تم استخدام مفتاح عدم التكرار مع طلب مختلف."),
        ["E-IDEMPOTENCY-IN-PROGRESS"] = ("A request with this Idempotency-Key is still being processed.", "طلب بنفس مفتاح عدم التكرار قيد المعالجة."),
        ["E-UNEXPECTED"] = ("An unexpected error occurred.", "حدث خطأ غير متوقع.")
    };
}
